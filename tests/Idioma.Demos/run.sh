#!/usr/bin/env bash
#
# Packs CodeChops.Idioma and runs each demo of demos/ against that package alone. A demo fetches the published package
# (its #:package line), so a demo written for a newer API than the published one fails for every visitor until that
# version is published. This run shows that the demos work with the package as it is packed now. CI runs it, and the
# publish workflow runs it before it publishes, so the demos on main work once the release is out.
#
# Push main together with the tag of its version: between a push of main and the release, a visitor's demo still gets
# the package published before.
#
# With IDIOMA_PACKED=true, it packs nothing and takes the package that is in artifacts/ already. The publish workflow
# runs it so on the package it pushes, which another job packed.

set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"

version="$(cd "$root" && dotnet msbuild src/Idioma/Idioma.csproj -getProperty:Version)"

if [ "${IDIOMA_PACKED:-}" = "true" ]; then
  if [ ! -f "$root/artifacts/CodeChops.Idioma.$version.nupkg" ]; then
    echo "IDIOMA_PACKED is true, but artifacts/ holds no CodeChops.Idioma.$version.nupkg to test." >&2
    exit 1
  fi
else
  (cd "$root" && dotnet pack src/Idioma/Idioma.csproj -c Release -o artifacts -warnaserror)
fi

# A copy of the demos next to a nuget.config that knows the packed package by a relative path, which also works in Git
# Bash on Windows, and takes the package from there alone. It has a packages folder of its own. So a
# package of the same version, restored earlier from another build, can't stand in for the one packed now.
work="$here/obj/demos"
rm -rf "$work"
mkdir -p "$work"
cp "$root"/demos/*.cs "$root/demos/Directory.Build.props" "$root/demos/global.json" "$work"

cat > "$work/nuget.config" <<CONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="packed" value="../../../../artifacts" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <!-- The package under test comes from the folder alone, never from nuget.org, where an older release of it lives. -->
  <packageSourceMapping>
    <packageSource key="packed">
      <package pattern="CodeChops.Idioma" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
CONFIG

export NUGET_PACKAGES="$work/packages"

cd "$work"

for demo in *.cs; do
    # The version packed now, where the demo takes the latest published one.
    sed -i.bak "s|#:package CodeChops.Idioma@\*|#:package CodeChops.Idioma@$version|" "$demo"

    if ! grep -q "#:package CodeChops.Idioma@$version" "$demo"; then
        echo "$demo doesn't take the package as '#:package CodeChops.Idioma@*', so this run can't point it at the packed one." >&2
        exit 1
    fi

    echo "--- $demo"
    dotnet run "$demo"
done
