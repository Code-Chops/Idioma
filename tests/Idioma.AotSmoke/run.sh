#!/usr/bin/env bash
#
# Packs CodeChops.Idioma, builds an app of this folder against that package alone with the compiler of .NET SDK 10
# (global.json here), publishes it with NativeAOT, and runs it. What the package says about trimming is then run, and the
# compiler it is said to work with is the one that builds against it, which the build of the repository, on a newer SDK,
# does not show.
#
# Needs the native toolchain of NativeAOT: on Ubuntu, `sudo apt-get install clang zlib1g-dev`. If the published program
# is stopped by the machine, `dotnet run --project AotSmokeApp` from here runs the same checks without NativeAOT.

set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"

# The repository's own SDK (its global.json) packs; the SDK of this folder builds the app.
(cd "$root" && dotnet pack src/Idioma/Idioma.csproj -c Release -o artifacts -warnaserror)
version="$(cd "$root" && dotnet msbuild src/Idioma/Idioma.csproj -getProperty:Version)"

cd "$here"
rm -rf obj/publish
dotnet publish AotSmokeApp/AotSmokeApp.csproj -c Release -o obj/publish "-p:IdiomaVersion=$version"

obj/publish/AotSmokeApp
