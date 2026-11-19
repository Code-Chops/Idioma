#!/usr/bin/env bash
#
# Packs CodeChops.Idioma and runs each demo of demos/ against that package alone. A demo fetches the published package
# (its #:package line), so a demo written for a newer API than the published one fails for every visitor until that
# version is published. This run shows that the demos work with the package as it is packed now. CI runs it, and the
# publish workflow runs it before it publishes, so the demos on main work once the release is out.
#
# Push main together with the tag of its version: between a push of main and the release, a visitor's demo still gets
# the package published before.

set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"

(cd "$root" && dotnet pack src/Idioma/Idioma.csproj -c Release -o artifacts -warnaserror)
version="$(cd "$root" && dotnet msbuild src/Idioma/Idioma.csproj -getProperty:Version)"

# A copy of the demos next to a nuget.config that knows the packed package, with a packages folder of its own. So a
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
    <add key="packed" value="$root/artifacts" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
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
