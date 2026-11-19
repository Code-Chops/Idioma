#!/usr/bin/env bash
#
# Runs every benchmark that docs/performance.md reports, and writes their tables to obj/results in this folder:
#
#   1. Showing a text on .NET, with BenchmarkDotNet.
#   2. The same in Blazor WebAssembly in a headless Chrome, on the Mono runtime with its interpreter.
#   3. The same with Mono's AOT, when the wasm-tools workload is installed (dotnet workload install wasm-tools).
#   4. The download sizes of a language of 100 texts, from Idioma.Benchmarks.Size, with Mono's interpreter and AOT.
#
# Takes about 20 minutes on a laptop. Needs Chrome: set CHROME=/path/to/chrome when it's installed elsewhere.

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

echo "1/4 Showing a text on .NET (BenchmarkDotNet)"
dotnet run -c Release --project Idioma.Benchmarks -- --filter '*' --artifacts "$results/benchmarkdotnet" > "$results/benchmarkdotnet.log"
cp "$results/benchmarkdotnet/results/Benchmarks.TextLookup-report-github.md" "$results/1-showing-a-text.md"

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

publish_size() {
  local name="$1"; shift
  dotnet publish Idioma.Benchmarks.Size -c Release -o "obj/size/$name" "$@" > "$results/publish-size-$name.log"
}

echo "4/4 Download sizes"
publish_size one-language -p:SecondLanguage=false
publish_size two-languages
dotnet "$runner" sizes obj/size/one-language/wwwroot obj/size/two-languages/wwwroot "with the interpreter" > "$results/4-download-sizes.md"
if $aot; then
  publish_size one-language-aot -p:SecondLanguage=false -p:RunAOTCompilation=true
  publish_size two-languages-aot -p:RunAOTCompilation=true
  echo >> "$results/4-download-sizes.md"
  dotnet "$runner" sizes obj/size/one-language-aot/wwwroot obj/size/two-languages-aot/wwwroot "with AOT" >> "$results/4-download-sizes.md"
fi

echo "The tables are in $results."
