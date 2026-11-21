# MoVibeSkua (MVS)

**[VibeSkua](https://github.com/NinjaXz/VibeSkua) for Android.** The Skua bot client for AdventureQuest Worlds, running on a phone: the same scripts and the same tools, with no PC required.

> [!IMPORTANT]
> **Download size:** `MVS_v1-0-4.apk`, about 63 MB.
>
> **64-bit ARM phones only.** The APK is built for `arm64-v8a`. It does not install on 32-bit phones, x86 tablets, Chromebooks with Intel or AMD processors, or PC emulators.
>
> | | Minimum | Recommended |
> | :--- | :--- | :--- |
> | **Android** | 8.0 | 12 or newer |
> | **Processor** | 64-bit ARM (arm64-v8a) | Upper mid-range or flagship chipset from 2022 or newer |
> | **Memory (RAM)** | 4 GB (one client) | 8 GB or more; allow about 1 GB per additional Army client |
> | **Free storage** | 500 MB (app, scripts and cached game files) | 1 GB |
> | **Other** | Up-to-date Android System WebView, internet connection | Stable Wi-Fi or mobile data |
>
> Reference device: Poco X6 Pro (Dimensity 8300-Ultra), where the game holds its 30 fps design target in busy maps.

> [!NOTE]
> **A message from Arglax**
>
> 1. If you are iffy with your data even if it's anonymous, it's fine, skip the app. It's made for personal use anyways.
>
> 2. Bot responsibly. It doesn't even hurt the game anymore at this point, given the bad design of their farmable items and stuff. Just don't get in the way of people who are *normally* farming manually. They deserve to pick and endure their own suffering and challenges, but they don't deserve to be teleported at the middle of the screen.

## Download and installation

1. Download `MVS_v1-0-4.apk` from the [latest release](../../releases/latest).
2. Open the file on your phone and allow "Install unknown apps" when Android asks.
3. Launch **MoVibeSkua**. On first run the app offers to download the community scripts; accept, then open **Repo** and press **Start** on any script.

## Using the app

Hold the phone sideways. The game fills the center of the screen and the controls sit on both edges.

The side buttons start as the game's own menus in a larger size: **Quests, Temp Inventory, Map, Lore, Menu, House, Settings** on the left; **Bag, Drops, Bank, Rest, Char, Quick** and **Guide** on the right. The **MVS crest** at the bottom right:

- **One tap** opens Support (Discord help, a note from the author, credits).
- **Two quick taps** switch the side buttons to the bot tools below; two more switch back.

| Left side | Right side |
| :--- | :--- |
| **Menu**: every Skua tool (options, bank, packets, plugins and more) | **Auto**: auto attack and hunt |
| **Accounts**: saved logins, server choice, automatic re-login | **Jump**: move between cells and pads, saved spots |
| **Scripts**: load, start, pause, edit options | **Loader / Grabber**: quests, shops, items |
| **Repo**: browse and download community scripts | **Options**: game options (lag killer and others) |
| **Logs**, **Army** | **Streamer**: hide your name, guild, gold and chat |
| **Start / Stop**: run the loaded script | |

**Background operation.** A notification keeps the bot running while the screen is off or another app is in use.

## Features

Every in-game change has its own switch in the game's **Options > Controls** (rows with the gold MVS tag).

<details>
<summary><b>Show all features</b> (select a group inside to see what it contains)</summary>
<br>

<details>
<summary><b>Game screen and touch controls</b></summary>

<!-- SCREENSHOT OWED: game screen with both side rails and the MVS crest (own character only, no other players' names). -->

- **Side buttons.** The game's menus as large buttons in the space beside the game. Two taps on the MVS crest swap them for the bot tools.
- **Mobile Layout.** Skill buttons arranged around auto attack, drops and potion in the skill bar, larger portraits that can be resized or hidden.
- **Dynamic Joystick.** The joystick appears wherever the thumb lands on the left side of the screen.
- **Long Dash.** The mobile dash travels three times as far.
- **Swipe scrolling** for the inventory, shops, quests, Settings and the chat, with flick scrolling.
- **Big menus.** The small menus of portraits, other players, Char and Quests open as large buttons.
- **Drops panel.** Drops wait in the skill bar and in the Drops panel (with Accept, Decline, Preview and Block) instead of appearing over the fight.
- **Script window.** While a script runs, a small window over the game shows its name, run time and current step.
- **Guides.** A game guide and a bot tools guide written in plain language.

</details>

<details>
<summary><b>Inventory and class ratings</b></summary>

<!-- SCREENSHOT OWED: inventory with class stars and the Sort chip (own character only). -->

- **Touch inventory.** Taller rows with class ranks, enhancement names and boost cards.
- **Gear sorting.** A Sort chip orders weapons, armor, helms and capes by damage type or boost.
- **Class star ratings.** Each class shows 0 to 5 stars for a chosen use case (Farming, Solo, Bossing, Ultra, PvP), with the best classes listed first.

</details>

<details>
<summary><b>Quick panel and travel</b></summary>

<!-- SCREENSHOT OWED: Quick panel, Actions tab and Travel tab. -->

- **Actions.** The game's own shortcut actions as buttons: users in the area, HP bars, hide players, monsters or the interface, friends, stats, target, rest and the chat keyboard.
- **Travel.** Join a map without typing `/join`: common places, a map name box, and a travel log of the maps visited, kept on the phone.
- **Floating button.** An optional button over the game that opens the panel.
- Opened from **Quick** on the right side or from the crest menu.

</details>

<details>
<summary><b>Jump and saved spots</b></summary>

<!-- SCREENSHOT OWED: Jump page with the Saved Spots list and the Sort menu. -->

- **Jump.** Move to any cell and pad of the current map.
- **Saved spots.** **Save where I stand** keeps the map (with its private room), cell and pad; one tap returns there later.
- **Sort.** Name A–Z, Map A–Z, Newest first or Oldest first.
- **Edit.** Move spots up or down, rename or delete them.
- The list is shared with Fast Travel, so both pages always show the same spots.

</details>

<details>
<summary><b>Auras overlay</b></summary>

<!-- SCREENSHOT OWED: both aura windows during a fight (own character and a monster only). -->

- **Two windows.** Your auras on the left and your target's at the top right, with stacks, timers to a tenth of a second and Decay / Enrage warnings.
- **Stack limits.** Stacks stop at the aura's limit (5/5); an aura with no listed limit shows only its count.
- **Who applied it.** Tap an aura for its details, including the player or monster that applied it.
- **Effects and filters.** Short effect labels, filters, pins and group order from the gear button.
- **Placement.** Windows stay inside the game picture by default; **Drag panels anywhere** lets them sit over the side rails.

</details>

<details>
<summary><b>Graphics and performance</b></summary>

<!-- SCREENSHOT OWED: Graphics & Performance page. -->

- **Presets.** Battery, Balanced, Quality and Ultra; starting values follow the phone's hardware.
- **Resolution, anti-aliasing and colors.** Each set on its own.
- **FPS meter.** Green at 28 fps and above, yellow from 20, red below.
- **Smooth motion.** Walking and sliding are drawn between the game's 30 frames so they look like 60.
- **Auto smooth.** When the frame rate drops, MVS steps down smooth motion, anti-aliasing and resolution one at a time, and restores them when there is room.
- **Heat handling.** Warnings from 45 °C battery temperature and a cooler mode at 47 °C or when Android reports the phone as hot.
- **Lag killer and crowd options.** Hide the game world while bots keep playing, hide or simplify other players, disable skill effects.
- **Bot-only and headless modes.** The game stops drawing to save battery while scripts keep running.
- **Engine improvements** in the bundled Ruffle build: lower memory use in busy maps and steadier frames.

</details>

<details>
<summary><b>Scripts</b></summary>

<!-- SCREENSHOT OWED: Scripts page with a loaded script and its options. -->

- **Repo.** Browse, search, download and update the full community script library.
- **Scripts.** Load, start, pause and stop scripts, and edit their options before starting.
- **Item dropdowns.** Script options that ask for an item name list the matching items you own.
- **Compiled on the phone.** Scripts are compiled on the device and cached, so later starts are fast.
- **CoreBots.** Per-account CoreBots options, and import of a `CBO_Storage` file copied from a PC.
- **Skill rotations.** Advanced Skills editor and built-in rotations checked against each class's mechanics.
- **Logs and Scheduler.** A step-by-step log of what the bot did, and scheduled script runs.
- **Problem reports.** When a script stops, MVS tells you whether the script or the app is at fault and where to report it.

</details>

<details>
<summary><b>Auto, Loader and Grabber</b></summary>

<!-- SCREENSHOT OWED: Auto page and Loader / Grabber page. -->

- **Auto.** Attack whatever is in the area, or Hunt chosen monsters across the map, with class stars to pick the best class.
- **Loader.** Open any shop or quest from anywhere by its ID.
- **Grabber.** Lists of shop items, quests and rewards, inventory, bank and the monsters on the map.
- **Use Best Enhancement.** Reads your class, weapon, helm and cape, shows the recommended enhancement for Farming, Bossing or Ultra, and changes only the pieces you tick, now or before each script.
- **More tools.** Bank, current drops, junk items, loadouts, stats, runtime and game options.

</details>

<details>
<summary><b>Accounts and Army (multi-client)</b></summary>

<!-- SCREENSHOT OWED: Army page with two clients (account names blurred). -->

- **Account Manager.** Saved accounts, encrypted with the Android Keystore; one tap logs in.
- **Automatic re-login** with a back-off when the login server stops answering.
- **Army.** Several game clients on one phone, each with its own login. Only the client on screen draws the game; the others keep playing and botting silently.
- **Switching.** Change clients from the Army page or from Android's recent-apps screen.
- **Army scripts** sync through the shared MVS folder, as on PC.

</details>

<details>
<summary><b>Butler (follow and summon)</b></summary>

<!-- SCREENSHOT OWED: Butler page (account names blurred). -->

- **Works across Army clients.** Butler clients running in Army on the same phone see each other as online.
- **Follow.** Followers stay with the master account, including **Follow master's quests** and **Copy master's walking**.
- **Summon all.** Brings every available butler to the master.
- **Send to player.** Sends this account and every available butler to a player, with a list of recent targets.
- Opened from the **Butler** button on the Army page or from the plugins menu.

</details>

<details>
<summary><b>Plugins, streamer mode and background running</b></summary>

- **Plugins** that ship with VibeSkua (Daily Tracker, ButlerV5, Cosmetics) open as pages.
- **Streamer mode.** Shows "MVS User" instead of your name and hides other players, your gold and the chat.
- **Discord and Nexus webhooks.**
- **Background running.** A notification with start, stop and pause controls and a farm summary keeps the bot running with the screen off.

</details>

</details>

## Free of charge

MoVibeSkua is completely free. It has no purchases, subscriptions, advertisements or paid features, and none are planned. Nobody is authorized to sell it; if you paid for it, ask for a refund.

Donations are optional and do not unlock anything:

- The author of MVS: [Buy Me a Coffee](https://buymeacoffee.com/arglaxaqwv) or [Ko-fi](https://ko-fi.com/arglaxgaming)
- The Skua team, whose work MVS is built on: [Skua donations](https://auqw.github.io/Skua-and-Grim-Donations/)

## Source code and contributing

MoVibeSkua is open source. The full source lives in [`src`](src) under the [MIT License](LICENSE): the Android app, the game page and in-game interface, the scripting core and the bundled plugins. It is published as a compact export of the author's working copy, without comments.

Bug reports, fixes, features and skill rotations are welcome from anyone. Please read the [contribution guide](CONTRIBUTING.md) first; it explains the layout, how to build the app and how changes are merged.

## Privacy

Your game login is sent only to Artix Entertainment's servers. MVS has no accounts of its own.

**Usage statistics.** MVS sends anonymous usage statistics so that its author can see what works: a random install identifier, app and Android version, phone model, processor, core count, memory, display refresh rate, WebView version, two-letter country (from the mobile network, SIM or phone region, never GPS or IP), frame rate, the app's memory use, battery temperature, session health and load times, and which buttons, pages and switches are used and for how long.

**Crash and problem reports.** When MVS crashes or runs into a problem of its own, it asks: "MVS has crashed. Send error report and crash log to the developer?"

- **Yes** sends the full report: the error and stack trace, where in the app it happened, a recent log of MVS's own activity, memory, frame-rate and temperature readings, MVS's settings, the anonymous install identifier, and your phone's specifications and country.
- **No** sends only the error and where it happened, with no install identifier and no device information.

Both answers are scrubbed before sending to remove account and character names, logins, passwords, tokens, e-mail addresses and user file paths. To stop the question and all crash report uploads, turn off **Ask me about crash reports** on the **Support** page. Errors caused by a script itself stay on the phone and are never sent.

**Never collected:** account names, logins, passwords, character or guild names, chat, script contents, IP address or location.

## Support

Questions and problems: [MVS Discord](https://discord.gg/Dwb7XMkpSh) or the [issue tracker](../../issues).

## Credits

MoVibeSkua is a side project by Arglax and stands on the work of others:

- **Skua** ([auqw/skua](https://github.com/auqw/skua)): Breno_Henrike (creator), Purple (maintainer), Tato (script team manager), Bogali, Vanilla99, Crow, Lonewolf_12 and the auqw script community.
- **VibeSkua** ([NinjaXz/VibeSkua](https://github.com/NinjaXz/VibeSkua)): Renoir Dessendre, creator of the VibeSkua branch this app builds on.
- **[ArrowDev](https://github.com/ArrowDev123/Skua)**: the early Avalonia port.
- **[Ruffle](https://github.com/ruffle-rs/ruffle)**: Flash emulation, licensed under MIT / Apache-2.0 (see [licenses](licenses)).
- **Class star ratings** are compiled from community guides: the [r/AQW Simplified Bosses Guide](https://docs.google.com/spreadsheets/d/1yytxo6x0XlvbTIYLLD6TCEF_v1EKV59e_N-gas9Fi8Y), [AQW Hub's class guide](https://www.aqwhub.com/other-guides/basic-class-guide/), [AQWG's ultra boss guides](https://sites.google.com/view/aqwg-net/ultra-bosses), the [AQWX community class setups](https://sites.google.com/view/aqwxcommunity-wiki-tutorial/class-setups), Artix's [PvP guide](https://www.aq.com/lore/guides/pvpguide) and [AQW World](https://www.aqwworld.com/2017/01/best-farming-class-on-aqw.html). Where no guide rates a class, the rating is the author's own estimate and is marked as such in the data. Treat the stars as a starting point, not a verdict.
- **[AQW Wiki](http://aqwwiki.wikidot.com)**: aura and enhancement descriptions (CC BY-SA 3.0).
- **Artwork.** The side-button pictures are AdventureQuest Worlds interface art, © Artix Entertainment, LLC. MoVibeSkua claims no rights in them or in any other game art, and the MIT License does not cover them.

## Disclaimer

MoVibeSkua is intended for educational and personal use. It is an unofficial, fan-made application that is not affiliated with, endorsed by or sponsored by Artix Entertainment, LLC, and it claims no ownership of the game, its assets or its data. AdventureQuest Worlds is a trademark of Artix Entertainment, LLC.

Automating gameplay may violate the AdventureQuest Worlds Terms of Service and may lead to action against your account, including a ban. The software is provided "as is", without warranty of any kind, and you use it entirely at your own risk.
