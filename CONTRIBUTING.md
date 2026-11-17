# Contributing to MoVibeSkua

Thank you for your interest in improving MoVibeSkua (MVS). This guide explains what can be contributed here and how.

## What this repository contains

`src/` holds the **full source of MVS**: the Android app, the game page and in-game interface, the scripting core and the bundled plugins.

| Folder | Purpose |
| :--- | :--- |
| `MoVibeSkua.Android` | The Android app: shell, pages, Army, crash reports, usage stats |
| `Skua.Flash.Ruffle` | The game page (Ruffle host, bridge, overlays, auras, quick panel) |
| `Skua.AS3` | `skua.swf`, the in-game interface |
| `Skua.App.Avalonia`, `Skua.Shared.Avalonia` | Views and view models shared with desktop VibeSkua |
| `Skua.Core`, `Skua.Core.Interfaces`, `Skua.Core.Models`, `Skua.Core.Utils`, `Skua.Core.Generators` | The scripting core: script API, skills, services |
| `Skua.Plugin.ButlerV5`, `Skua.Plugin.DailyTracker`, `Skua.Plugin.Cosmetics` | The bundled plugins |
| `tools` | Build and development helpers |

The code in `src/` is a generated export of the maintainer's working tree with comments and blank lines removed, so it is compact by design. Builds you make yourself send no usage statistics.

## What to contribute

- Fixes to script API behaviour (wrong results, crashes, Android-specific problems such as file-name case).
- Skill rotations in `src/Skua.Core/Skills/MvsCoreSkills.json`.
- Compatibility fixes for community scripts.
- Fixes and improvements anywhere in the app: pages, the game page, overlays, Army, plugins.
- Bug reports and feature requests.

**Scripts themselves do not belong here.** Send them to the Skua script community (see the credits in the [README](README.md)).

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) with the Android workload, and the Android SDK.

```bash
cd src
./build-android.sh Release
```

Release builds are signed, so create your own key first: put a keystore and a `signing.properties` file (`storeFile`, `storePassword`, `keyAlias`, `keyPassword`) in `src/signing/`. Android will not install an APK signed with your key over the official MoVibeSkua, or the official one over yours: uninstall one before installing the other.

For the scripting core alone: `dotnet build src/Skua.Core -c Release`.

A pull request must build without errors.

## Reporting a problem

Open an [issue](../../issues) with:

1. App version (crest > Support, bottom line) and phone model.
2. What you did, what you expected and what happened.
3. The script name, if a script was running.
4. A log excerpt or screenshot where possible. Remove account names first.

Never post logins, passwords or tokens.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep the change focused: one fix or feature per pull request.
3. Follow the style of the surrounding code (`.editorconfig` is included). Comments are welcome in a pull request; they are removed when the public copy is regenerated.
4. Describe what the change does and how you tested it.
5. By submitting a pull request you agree that your contribution is licensed under the [MIT License](LICENSE).

### How changes are merged

Because `src/` is generated, accepted pull requests are applied to the maintainer's working tree and appear in this repository with the next export. Your pull request is then closed with a reference to that commit, and you are credited by name in the commit message and the release notes.

## Conduct

Be respectful and constructive. Harassment, account selling and requests for help with scamming or stealing accounts are not tolerated.

## Questions

Ask in the [MVS Discord](https://discord.gg/Dwb7XMkpSh).
