using System.Collections.Generic;
using MoVibeSkua.Android.ViewModels;
namespace MoVibeSkua.Android.Views;
public static class GuideContent
{
private static readonly (string Title, string Text) PrivateRoom =
("Private room", "A copy of a map with its own number, like battleon-1234. Only players who type the same number end up with you.");
private static readonly (string Title, string Text) Script =
("Script", "A ready-made bot: a list of things for your character to do, like farming an item or finishing quests. You do not need to read or write it. You only start it.");
private static readonly Dictionary<string, (string Title, string Text)> GameTips = new()
{
["tag"] = ("The gold MVS tag", "Options that MVS added carry a small gold tag that says MVS. Anything without the tag is the game's own option."),
["mobile"] = ("Mobile Controls", "The game's own touch controls: a joystick on the left and skill buttons on the right. You find it in the game's Options, under Controls."),
["stars"] = ("Star rating", "0 to 5 stars per class, gathered from community guides for farming, solo play, bosses and PvP. Where no guide rates a class, the stars are an estimate. Use them as a starting point, not a rule."),
["stacks"] = ("Stacks", "Some auras build up. Each use adds one stack, and more stacks make the effect stronger. 3/5 means 3 stacks out of a maximum of 5."),
["decay"] = ("Decay", "A debuff that blocks healing. While it is on you, heals and potions do nothing."),
["taunt"] = ("Taunt and enrage", "A taunt makes the monster attack one player. Enrage powers the monster up, so it hits harder."),
["log"] = ("Travel log", "A list of the maps you visited, newest first. It is kept on this phone only. Tap a map to go back there, or × to remove it."),
["room"] = PrivateRoom,
["auto"] = ("Auto smooth", "MVS watches how smoothly the game runs. When it stays below 30 FPS for a few seconds, graphics go down one step at a time. When there is room again, they go back to your settings."),
["fps"] = ("FPS", "Frames per second: how many pictures the game draws each second. The game is made for 30. Below 20 it feels choppy."),
["headless"] = ("Headless mode", "The screen goes black and nothing is drawn, but the game and any running bot keep going, and you stay logged in. It saves battery and keeps the phone cool. Tap anywhere to bring the picture back."),
["bottools"] = ("Bot tools", "MVS's other set of side buttons: Scripts, Repo, Auto and more. They let MVS play the game for you. The bot tools guide explains each one."),
["script"] = Script,
["stats"] = ("Anonymous usage numbers", "Things like phone model, frame rate, how warm the phone got and which buttons get used. They help MVS's maker see what works. Never your name, login, character, scripts, maps or chat."),
["report"] = ("Crash report", "Yes sends the error together with your phone's specs and country. No sends only the error itself. Account and character names, passwords and e-mail addresses are always taken out.")
};
private static readonly Dictionary<string, (string Title, string Text)> BotTips = new()
{
["script"] = Script,
["cell"] = ("Cell", "One screen of a map. Maps are made of several cells, and you walk from one to the next."),
["room"] = PrivateRoom,
["id"] = ("ID", "The number the game uses for a quest, a shop or an item. Every quest, shop and item has one."),
["client"] = ("Client", "One running copy of the game with its own login. Each client plays one account."),
["enh"] = ("Enhancement", "An upgrade on a class, weapon, helm or cape that adds stats. Which one is best depends on what you do: farming, bosses or ultra bosses."),
["botonly"] = ("Bot only", "The game is hidden and nothing is drawn, to save power and heat. The bot keeps running. Tap Show game to bring the picture back.")
};
private static readonly Dictionary<string, (string Title, string Text)> PageTips = new()
{
["ram"] = ("RAM", "Phones report a little less memory than the box says, so MVS rounds up to the nearest size sold, such as 4, 6 or 8 GB."),
["chip"] = ("Processor", "MVS needs a 64-bit ARM processor, which almost every phone from the last several years has. The number of cores and the top speed in GHz are shown for your information."),
["webview"] = ("Android System WebView", "The part of Android that draws the game in MVS. Its age is estimated from its version number. To update it, open the Play Store and search for Android System WebView."),
["pct"] = ("Percent of recommended", "This phone's number divided by the recommended number. 100% means exactly the recommended; more is spare room. It compares with MVS's requirements, not with other phones."),
["steady"] = ("Steady frame rate", "The game draws its pictures at an even pace, so movement does not stutter. MVS holds the pace at the Target FPS you pick."),
["locked"] = ("Locked enhancement", "When the best enhancement for a piece is still locked for you, MVS names it and uses the best one you have unlocked. A Forge enhancement the piece already has is kept."),
["best"] = ("Where \"best\" comes from", "From the script team's SmartEnhance list, the same list their scripts use. For Ultra bosses, from the r/AQW Simplified Bosses Guide."),
["team"] = ("Script team", "The people who write the scripts: the Skua, VibeSkua and Tato team (AUQW). They are not part of MVS, so script problems go to their Discord.")
};
public static readonly IReadOnlyDictionary<string, (string Title, string Text)> Tips = Merge(GameTips, BotTips, PageTips);
private static Dictionary<string, (string Title, string Text)> Merge(params Dictionary<string, (string Title, string Text)>[] sets)
{
Dictionary<string, (string Title, string Text)> all = new();
foreach (Dictionary<string, (string Title, string Text)> set in sets)
{
foreach (KeyValuePair<string, (string Title, string Text)> tip in set)
all[tip.Key] = tip.Value;
}
return all;
}
public static GuideDoc Game(GuideViewModel vm) => new GuideDoc("MVS Guide")
.Section("The Sidebar", "The Sidebar")
.P("These are the game's own menus, made big enough to tap: Quests, Map, Bag, Bank and the rest.")
.Bullets(
"*Drops* holds the loot waiting for you, and shows a red number when something dropped.",
"*Quick* opens shortcuts and travel.",
"*Guide* opens this page.",
"*MVS crest* in the corner opens Support, and swaps the Sidebar to the bot tools.")
.Section("MVS crest", "The MVS crest")
.P("The round MVS badge in the corner.")
.Bullets(
"*One tap* opens *Support MVS*: help, crash report choices and credits.",
"*Two quick taps* swap the Sidebar to the [bottools|bot tools]. Two quick taps again bring the game's buttons back.")
.Note("With *Sidebar* switched off, one tap shows or hides the side buttons instead.")
.Section("Switches", "Turning MVS's changes on and off")
.P("Open the game's *Options*, then *Controls*. Every row with a [tag|gold MVS tag] is one of ours. Tap a row to switch it, or tap *(?)* to see what it does.")
.Bullets("*MVS UI* is the main switch: turn it off and you get the game exactly as it normally looks.")
.Note("*Bigger UI* and *Long Dash* need a restart to take effect.")
.Section("Touch (MFUI)", "Playing with touch (MFUI)")
.Steps(
"Set AQW's [mobile|Mobile Controls] option to {g|On}.",
"Set MVS's *Mobile Layout* to {g|On} to put the skill buttons around the attack button.")
.Bullets(
"*Dynamic Joystick* lets you put your thumb anywhere on the left side to walk.",
"*Long Dash* makes the dash go three times as far.",
"*Swipe Scrolling* lets you swipe through lists, shops, quests and the chat.")
.Section("Bag", "Bag")
.P("Taller rows that are easier to tap.")
.Bullets(
"Classes show their *rank* and a [stars|star rating].",
"Gear shows its *enhancement* and *boosts*.")
.P("To sort your gear:")
.Steps(
"Open the *Bag*.",
"Tap *Sort* next to the title.",
"Pick a damage type or a boost.")
.P("To put your best classes first:")
.Steps(
"Open the *Bag* on your classes.",
"Tap the *MVS:* chip next to the title.",
"Pick Farming, Solo, Bossing, Ultra or PvP.")
.Section("Auras", "Auras (buffs and debuffs)")
.P("Two small windows: *yours* on the left, your *target's* at the top right. Each line shows the aura, its [stacks|stacks], the time left and who applied it.")
.Bullets(
"{r|Red} blocks healing, like [decay|Decay].",
"{o|Orange} is a [taunt|taunt or enrage].",
"{b|Blue glow} means it is at its maximum.")
.P("Tap a line to see more. Drag a window by its title to move it.")
.Bullets(
"*–* shrinks the window, *×* hides it.",
"*⇆* joins the two windows, or splits them again.",
"*A* changes the text size, *⚙* has pins and filters.")
.Note("To bring back a hidden window, tap a portrait, then *Show Auras*.")
.Section("Quick", "Quick: shortcuts and travel")
.Steps(
"Tap *Quick* on the right side.",
"Pick a tab: *Actions* or *Travel*.")
.Bullets(
"*Actions*: one-tap shortcuts. See who is in the room, show HP bars, hide other players, rest, open the chat.",
"*Travel*: go to a map without typing /join. Pick a place, type a map name, or tap one in your [log|travel log].",
"Add a number for a [room|private room], like battleon-1234.")
.Note("The game makes you wait 5 seconds between maps.")
.Section("Drops", "Drops")
.P("Loot waits in the Drops panel instead of popping up in the middle of a fight.")
.Steps(
"Tap *Drops* on the right side.",
"Tap *Accept* or *Decline* on each item.")
.Section("Performance", "Performance Issues")
.Steps(
"Tap the *MVS crest* twice to show the bot tools.",
"Tap *Menu*, then *Graphics & Performance*.",
"Pick *Battery* or *Balanced*.")
.P("Also useful:")
.Bullets(
"[auto|Auto smooth] lowers the graphics only while the game struggles.",
"*Show game FPS* shows how smoothly the game runs. An [fps|FPS] of 30 or more is good.",
"[headless|Headless mode] turns the picture off but keeps you logged in. Tap the crest once, then *Headless mode*.")
.Section("Back button", "Back button")
.Bullets(
"A page is open: *Back* closes it.",
"Nothing is open: *Back* sends MVS to the background. You stay connected, and a running bot keeps going.")
.Section("Bots", "Let MVS play for you")
.P("MVS can run ready-made bots, called [script|scripts], that farm, quest and fight for you.")
.Buttons(true, ("Open the bot tools guide", vm.OpenBotGuideCommand))
.Section("Privacy", "Privacy")
.P("MVS sends [stats|anonymous usage numbers]. Never your name, login or chat.")
.P("If MVS crashes, it asks before sending a [report|crash report]. To stop it asking:")
.Steps(
"Tap the *MVS crest* once.",
"Switch off *Ask me about crash reports*.")
.Note("With that off, MVS never sends a crash report.")
.Buttons(false, ("Bot tools", vm.SwitchRailsCommand), ("Support MVS", vm.OpenSupportCommand));
public static GuideDoc Bot(BotGuideViewModel vm) => new GuideDoc("Bot Tools")
.Section("First bot", "Your first bot")
.P("A [script|script] is a ready-made bot. You only pick one and start it.")
.Steps(
"Tap the *MVS crest* twice to show the bot tools.",
"Tap *Repo*, then *Download all*.",
"Find a script and tap its {g|Start}.",
"Tap {r|Stop} at the bottom left to end it.")
.Note("While a bot runs, a small window over the game shows what it is doing.")
.Section("Bot tools", "Showing the bot tools")
.Steps(
"Tap the *MVS crest* twice, quickly. The Sidebar now shows the bot tools.",
"Tap twice again to get the game's buttons back.")
.Note("Going back to the game's buttons closes any open bot page.")
.Section("Scripts", "Scripts")
.P("The script you loaded, its settings and its log.")
.Bullets(
"*Load Script* picks a script you already downloaded.",
"*Edit Script Options* changes its settings, like how many of an item to farm.",
"{g|Start} at the bottom left runs it, and turns into {r|Stop}.",
"*Logs* lists every step the bot took. If a script stops early, the reason is in the last lines.")
.Note("Most scripts change classes by themselves. Set your classes once in *Menu*, under *CoreBots*.")
.Section("Repo", "Repo")
.P("The library of scripts made by the community.")
.Bullets(
"Type in the search box to find a script by name.",
"{g|Start} runs a script now. *Load* only gets it ready in *Scripts*.",
"*Download all* gets every script. *Update all* gets the newest version of the ones you have.")
.Section("Auto", "Auto: fight without a script")
.Steps(
"Walk to the monsters you want.",
"Tap *Auto*.",
"Tap *Auto Attack* or *Auto Hunt*.",
"Tap *Stop Automation* to end it.")
.Bullets(
"*Auto Attack* fights in the [cell|cell] you are in.",
"*Auto Hunt* moves between cells to find monsters.")
.Section("Jump", "Jump")
.P("Travel without walking, and keep the places you use often.")
.Steps(
"Tap *Jump*.",
"Tap *Save where I stand*, or type a map and a [room|room] and tap *Add*.",
"Tap *Jump* on a saved spot to go there.")
.Section("Loader", "Loader and Grabber")
.P("Open a shop or a quest from anywhere:")
.Steps(
"Tap *Loader*.",
"Pick *Shop* or *Quests*.",
"Type its [id|ID] and tap *Load*.")
.Bullets("*Grabber* lists what the game has open: the items in a shop, quests and their rewards, your bag and bank, the monsters on the map. Tap *Grab* to fill a list.")
.Section("Accounts", "Accounts")
.P("Save a login once, then log in with one tap.")
.Steps(
"Tap *Accounts*, then *Add account*.",
"Type the username and password, and pick a server.",
"Tap *Save & log in*.")
.Bullets(
"*Log in* on a saved account logs in with it.",
"If you get disconnected, MVS logs you back in.")
.Note("Saved accounts stay on this phone only, encrypted.")
.Section("Army", "Army")
.P("Several accounts playing at once. Each one runs in its own copy of the game, called a [client|client].")
.Steps(
"Tap *Army*, then *Add client*.",
"Log in on the new client with another account.",
"Tap *Switch* to move between clients.")
.Bullets("Only the client on screen draws the game. The others keep playing without a picture, and stay silent.")
.Note("Every client needs a lot of memory. MVS warns you when the phone is running low.")
.Section("Butler", "Butler")
.P("Makes your accounts follow each other and fight together.")
.Steps(
"Tap *Army*, then *Butler*.",
"Tap *Settings* and turn on *Enable Butler*.",
"On the account that leads, tap *Summon all*.")
.Bullets("*Stop following* ends it on a follower. *Release all* lets the leader's followers go.")
.Note("An account that is running a script cannot follow. Stop the script first.")
.Section("Options", "Options: best enhancements")
.P("Puts the best [enh|enhancements] for your class on your gear.")
.Steps(
"Tap *Options*.",
"Pick Farming, Bossing or Ultra, then tap *Read my gear*.",
"Tick the pieces you want changed.",
"Tap *Enhance ticked items now*.")
.Bullets(
"*Before each script* does this by itself every time a script starts.",
"*Game options*, further down, has game tweaks the bot can apply, like making the game lighter or hiding other players.")
.Note("Unticked pieces are never touched.")
.Section("Streamer", "Streamer")
.P("Hides who you are, for streams and screenshots.")
.Steps(
"Tap *Streamer* to switch it {g|On}.",
"Tap it again to switch it off.")
.Bullets(
"Your name shows as *MVS User* and your guild as *MVS*.",
"The chat is hidden.")
.Section("Background", "Running in the background")
.P("A bot keeps running when you leave MVS for another app.")
.Bullets(
"A notification shows the script, how long it has run and what it earned.",
"Its buttons: *Pause*, {r|Stop}, *Bot only* and *Close*.",
"[botonly|Bot only] hides the game to save battery. The moon button on the script window does the same.")
.Note("The first time you start a script, Android asks to allow notifications. Say yes, or you will not see this.")
.Section("Good to know", "Good to know")
.Bullets(
"Botting can go against AQW's rules, and accounts can get banned. You use it at your own risk.",
"Scripts are made by the script team (Skua, VibeSkua, Tato), not by MVS. A script problem goes to their Discord.",
"A problem with the app itself goes to the MVS Discord: tap the crest once.")
.Buttons(false, ("Show the bot tools", vm.ShowBotToolsCommand), ("Script team Discord", vm.OpenAuqwDiscordCommand));
}
