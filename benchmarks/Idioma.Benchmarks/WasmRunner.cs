using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Benchmarks;

/// <summary>
/// Serves a published Idioma.Benchmarks.Wasm to a headless Chrome, page after page, and prints what the pages report.
/// </summary>
/// <remarks>
/// Each page runs every scenario for one culture. It posts its results to /report, and the answer names the next page
/// to load.
/// </remarks>
internal static class WasmRunner
{
    public sealed record Options(string? Chrome, string Label)
    {
        public static Options Parse(string[] options)
        {
            var parsed = new Options(Chrome: Environment.GetEnvironmentVariable("CHROME"), Label: "Blazor WebAssembly");

            for (var i = 0; i + 1 < options.Length; i += 2)
            {
                parsed = options[i] switch
                {
                    "--chrome" => parsed with { Chrome = options[i + 1] },
                    "--label" => parsed with { Label = options[i + 1] },
                    _ => throw new ArgumentException($"Unknown option '{options[i]}'. Use --chrome or --label."),
                };
            }

            return parsed;
        }
    }

    public static async Task RunAsync(string wwwroot, Options options)
    {
        wwwroot = Path.GetFullPath(wwwroot);
        if (!File.Exists(Path.Combine(wwwroot, "index.html")))
            throw new ArgumentException($"{wwwroot} has no index.html. Pass the wwwroot folder of a published Idioma.Benchmarks.Wasm.");

        // The first culture twice, and the first report is dropped. Chrome compiles WebAssembly in tiers, and the first
        // page of a fresh browser ran before the optimized code was there: up to a third slower on the CoreCLR runtime.
        var pages = new Queue<string>();
        pages.Enqueue($"?culture={Scenarios.Cultures[0]}");
        foreach (var culture in Scenarios.Cultures)
            pages.Enqueue($"?culture={culture}");

        var port = FindFreePort();
        var origin = $"http://127.0.0.1:{port}/";
        var server = new Server(wwwroot, pages);

        using var listener = new HttpListener();
        listener.Prefixes.Add(origin);
        listener.Start();
        _ = server.ServeAsync(listener);

        using var chrome = StartChrome(options.Chrome, origin + pages.Dequeue());

        try
        {
            await server.Finished(pageTimeout: TimeSpan.FromSeconds(120));
        }
        finally
        {
            chrome.Kill(entireProcessTree: true);
        }

        Print([.. server.Reports.Skip(1)], options);
    }

    private sealed class Server(string wwwroot, Queue<string> pages)
    {
        private readonly SemaphoreSlim reported = new(0);
        private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock gate = new();

        public List<string> Reports { get; } = [];

        public async Task ServeAsync(HttpListener listener)
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception) when (!listener.IsListening)
                {
                    return;
                }

                _ = Task.Run(() => this.HandleAsync(context));
            }
        }

        /// <summary>
        /// Waits for the last page, and fails when a page reports an error or doesn't report in time.
        /// </summary>
        public async Task Finished(TimeSpan pageTimeout)
        {
            while (!this.finished.Task.IsCompleted)
            {
                var progress = this.reported.WaitAsync(pageTimeout);
                if (await Task.WhenAny(progress, this.finished.Task) == progress && !await progress)
                    throw new TimeoutException($"No page reported for {pageTimeout.TotalSeconds} seconds. Open the page in a browser to see what fails.");
            }

            await this.finished.Task;
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // Cross-origin isolation, which gives performance.now(), and so Stopwatch, a finer resolution.
                response.AddHeader("Cross-Origin-Opener-Policy", "same-origin");
                response.AddHeader("Cross-Origin-Embedder-Policy", "require-corp");
                response.AddHeader("Cache-Control", "no-store");

                if (request.HttpMethod is "POST" && request.Url!.AbsolutePath is "/report")
                {
                    using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                    var report = await reader.ReadToEndAsync();
                    await this.WriteAsync(response, "text/plain", Encoding.UTF8.GetBytes(this.Next(report)));
                    return;
                }

                var path = request.Url!.AbsolutePath is "/" ? "index.html" : Uri.UnescapeDataString(request.Url.AbsolutePath.TrimStart('/'));
                var file = Path.GetFullPath(Path.Combine(wwwroot, path));

                if (!file.StartsWith(wwwroot + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(file))
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                await this.WriteAsync(response, ContentType(file), await File.ReadAllBytesAsync(file));
            }
            catch (Exception exception)
            {
                this.finished.TrySetException(exception);
            }
        }

        private string Next(string report)
        {
            if (report.Split('\n').FirstOrDefault(line => line.StartsWith("error\t", StringComparison.Ordinal)) is { } error)
                this.finished.TrySetException(new InvalidOperationException($"A page failed: {error[6..]}"));

            lock (this.gate)
            {
                this.Reports.Add(report);
                this.reported.Release();

                if (pages.TryDequeue(out var next))
                    return "/" + next;
            }

            this.finished.TrySetResult();
            return "";
        }

        private async Task WriteAsync(HttpListenerResponse response, string contentType, byte[] body)
        {
            response.ContentType = contentType;
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body);
            response.Close();
        }

        private static string ContentType(string file) => Path.GetExtension(file) switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" or ".mjs" => "text/javascript",
            ".wasm" => "application/wasm",
            ".json" => "application/json",
            ".css" => "text/css",
            _ => "application/octet-stream",
        };
    }

    private static Process StartChrome(string? chrome, string url)
    {
        chrome ??= FindChrome() ?? throw new InvalidOperationException("Chrome wasn't found. Pass its path with --chrome, or set CHROME.");

        var start = new ProcessStartInfo(chrome) { RedirectStandardError = true, RedirectStandardOutput = true };
        string[] arguments =
        [
            "--headless",
            $"--user-data-dir={Directory.CreateTempSubdirectory("idioma-benchmarks-chrome").FullName}",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-extensions",
            "--disable-background-timer-throttling",
            "--disable-renderer-backgrounding",
            url,
        ];

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        var process = Process.Start(start) ?? throw new InvalidOperationException($"{chrome} didn't start.");
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private static string? FindChrome()
    {
        string[] candidates = OperatingSystem.IsMacOS()
            ? ["/Applications/Google Chrome.app/Contents/MacOS/Google Chrome", "/Applications/Chromium.app/Contents/MacOS/Chromium"]
            : OperatingSystem.IsWindows()
                ? [@"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"]
                : ["/usr/bin/google-chrome", "/usr/bin/google-chrome-stable", "/usr/bin/chromium", "/usr/bin/chromium-browser"];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static int FindFreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    private static void Print(List<string> reports, Options options)
    {
        var lines = reports.SelectMany(report => report.Split('\n', StringSplitOptions.RemoveEmptyEntries)).Select(line => line.Split('\t')).ToArray();

        var browser = lines.FirstOrDefault(fields => fields[0] is "browser")?[1] ?? "an unknown browser";
        var runtime = lines.FirstOrDefault(fields => fields[0] is "runtime")?[1] ?? "an unknown runtime";

        Console.WriteLine($"{options.Label}: {runtime}, in {browser}.");
        Console.WriteLine();
        Console.WriteLine("Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:");
        Console.WriteLine();
        Console.WriteLine($"| Text | Approach | {string.Join(" | ", Scenarios.Cultures)} |");
        Console.WriteLine($"|---|---|{string.Concat(Scenarios.Cultures.Select(_ => "---:|"))}");

        var scenarios = lines.Where(fields => fields[0] is "scenario").ToArray();
        foreach (var group in scenarios.GroupBy(fields => (Text: fields[2], Approach: fields[3])))
        {
            var cells = Scenarios.Cultures.Select(culture => group.FirstOrDefault(fields => fields[1] == culture)?[4] ?? "–");
            Console.WriteLine($"| {group.Key.Text} | {group.Key.Approach} | {string.Join(" | ", cells)} |");
        }

        var floors = lines.Where(fields => fields[0] is "floor").Select(fields => $"{fields[1]} {fields[2]} ns");
        Console.WriteLine();
        Console.WriteLine($"The loop and the call cost: {string.Join(", ", floors)}.");
    }
}
