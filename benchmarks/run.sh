#!/usr/bin/env bash
#
# Runs every benchmark that docs/performance.md reports. It writes their tables to results in this folder, which the
# repository keeps, and their logs to obj/results:
#
#   1. Showing a text on .NET, with BenchmarkDotNet.
#   2. The same in Blazor WebAssembly in a headless Chrome, on the Mono runtime with its interpreter.
#   3. The same with Mono's AOT, when the wasm-tools workload is installed (dotnet workload install wasm-tools).
#   4. The download sizes of each approach, of a language of 100 texts, and of a language loaded lazily, from
#      Idioma.Benchmarks.Size, with Mono's interpreter and AOT.
#   5. Finding a label on .NET, with BenchmarkDotNet, in the run of step 1.
#
# Takes about 12 minutes on a laptop, most of it in step 1. Needs Chrome: set CHROME=/path/to/chrome when it's installed
# elsewhere. To compare two versions of the code, run only the benchmarks you need with --quick instead, as
# docs/performance.md shows.

set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
results="$here/obj/results"
cd "$here"

# Emptied first, because a publish leaves the fingerprinted files of an earlier one, which the download sizes would count.
rm -rf "$results" obj/wasm-interpreter obj/wasm-aot obj/size
mkdir -p "$results"

runner=Idioma.Benchmarks/bin/Release/net11.0/Idioma.Benchmarks.dll

# Fails at once when an approach shows other wording, rather than after the benchmarks of step 1.
dotnet build Idioma.Benchmarks -c Release > "$results/build.log"
dotnet "$runner" check

echo "1/4 Showing a text and finding a label on .NET (BenchmarkDotNet)"
dotnet run -c Release --project Idioma.Benchmarks -- --filter '*' --artifacts "$results/benchmarkdotnet" > "$results/benchmarkdotnet.log"
cp "$results/benchmarkdotnet/results/Benchmarks.TextLookup-report-github.md" "$results/1-showing-a-text.md"
cp "$results/benchmarkdotnet/results/Benchmarks.LabelLookup-report-github.md" "$results/5-finding-a-label.md"

echo "2/4 Blazor WebAssembly, Mono interpreter"
dotnet publish Idioma.Benchmarks.Wasm -c Release -o obj/wasm-interpreter > "$results/publish-wasm-interpreter.log"
dotnet "$runner" wasm obj/wasm-interpreter/wwwroot --label "Blazor WebAssembly, Mono interpreter" > "$results/2-wasm-interpreter.md"

aot=false
if dotnet workload list | grep -q wasm-tools; then
  aot=true
fi

echo "3/4 Blazor WebAssembly, Mono AOT"
if $aot; then
  dotnet publish Idioma.Benchmarks.Wasm -c Release -p:RunAOTCompilation=true -o obj/wasm-aot > "$results/publish-wasm-aot.log"
  dotnet "$runner" wasm obj/wasm-aot/wwwroot --label "Blazor WebAssembly, Mono AOT" > "$results/3-wasm-aot.md"
else
  echo "    Skipped, because AOT needs the wasm-tools workload: dotnet workload install wasm-tools"
fi

# Publishes the app without localization, with each approach in one language and in two, and with Idioma's second
# language in an assembly the app loads lazily.
publish_sizes() {
  local suffix="$1"; shift
  dotnet publish Idioma.Benchmarks.Size -c Release -o "obj/size/none$suffix" -p:SizeApproach=none "$@" > "$results/publish-size-none$suffix.log"
  for approach in idioma localizer; do
    dotnet publish Idioma.Benchmarks.Size -c Release -o "obj/size/$approach-one$suffix" -p:SizeApproach=$approach -p:SecondLanguage=false "$@" > "$results/publish-size-$approach-one$suffix.log"
    dotnet publish Idioma.Benchmarks.Size -c Release -o "obj/size/$approach-two$suffix" -p:SizeApproach=$approach "$@" > "$results/publish-size-$approach-two$suffix.log"
  done
  dotnet publish Idioma.Benchmarks.Size -c Release -o "obj/size/idioma-lazy$suffix" -p:SizeApproach=idioma -p:LazyLanguage=true "$@" > "$results/publish-size-idioma-lazy$suffix.log"
}

echo "4/4 Download sizes"
publish_sizes ""
dotnet "$runner" sizes obj/size "" "with the interpreter" > "$results/4-download-sizes.md"
if $aot; then
  publish_sizes -aot -p:RunAOTCompilation=true
  echo >> "$results/4-download-sizes.md"
  dotnet "$runner" sizes obj/size -aot "with AOT" >> "$results/4-download-sizes.md"
fi

# The tables alone go to a folder the repository keeps, next to the logs it doesn't. Commit them with the numbers of
# docs/performance.md, so that each number there can be traced to the run it comes from.
kept="$here/results"
mkdir -p "$kept"
rm -f "$kept"/*.md
cp "$results"/[1-5]-*.md "$kept"

echo "The tables are in $kept, and the logs in $results."
