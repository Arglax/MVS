# Script audit

Compiles every script in the CoreBots repo against the libraries the phone compiles against,
with the same default namespaces and the same prelude, and lists what does not compile.
Run it after a change to Skua.Core, the compat layer or the prelude, and before a release.

1. Build the app (`./build-android.sh Release`).
2. Unpack its reference libraries: `unzip -j <apk> "assets/refs/*" -d refs`
3. Get the scripts: `git clone --depth 1 -b Skua https://github.com/auqw/Scripts.git`
4. `dotnet run -c Release -- <Scripts> <refs> ../../Skua.Core/AppStartup/Services.cs ../../MoVibeSkua.Android/Platform/ScriptShims.cs audit.txt`

All scripts are compiled together, so errors that only come from that (two scripts defining the
same class) are left out. Known leftovers on 2026-11-19: four template files with placeholders
and `SkuaScriptsGenerator/Program.cs`, which are broken or not scripts on desktop too.
