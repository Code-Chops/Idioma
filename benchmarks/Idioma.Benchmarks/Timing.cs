using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;

namespace Benchmarks;

/// <summary>
/// The job of every benchmark on .NET: three launches of 5 warm-up and 15 measured iterations, of at least 100 ms each.
/// With <c>--quick</c>, one launch of 3 warm-up and 10 measured iterations, to compare two versions of the code on one
/// machine. The tables in <c>results</c> come from a full run.
/// </summary>
// Three launches, because the time of a lookup varies from one process to the next more than within one, and the error
// of a single launch doesn't show that. Iterations of 100 ms, not BenchmarkDotNet's 500 ms: a text takes a few
// nanoseconds, so one iteration still shows it some 30 million times, and a full run takes a fifth of the time.
public sealed class Timing : ManualConfig
{
    /// <summary>
    /// The environment variable that <c>--quick</c> sets, which the processes of the launches inherit.
    /// </summary>
    public const string QuickVariable = "IDIOMA_BENCHMARKS_QUICK";

    public Timing()
    {
        var job = Job.Default.WithIterationTime(TimeInterval.FromMilliseconds(100));

        job = IsQuick
            ? job.WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(10)
            : job.WithLaunchCount(3).WithWarmupCount(5).WithIterationCount(15);

        this.AddJob(job);
    }

    private static bool IsQuick => String.Equals(Environment.GetEnvironmentVariable(QuickVariable), "true", StringComparison.OrdinalIgnoreCase);
}
