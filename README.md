# Wells Anti Cheat

A host-side moderation mod for Among Us. It protects your lobby and does **nothing** unless you are the host — and it never acts against you.

## Features

**Detection (auto-kick when you're host):**
- **Offensive / banned names** — leetspeak-aware filter. Includes `Antipride` by default plus a slur list; matching catches `n1gg3r`, `n i g g e r`, `niiigger`, etc. Blocklist is an editable text file, no recompile needed.
- **Chat spam** — kicks players who send too many messages in a short window (default 5 / 3s).
- **Crash attempts** — catches malformed RPCs (too-short payloads), RPC floods (default 50 / 1s), and oversized chat messages, all common client/server crashers.
- **RPC anti-cheat** — illegal vents, teleports (SnapTo) in the lobby, impostors completing tasks, start-counter spoofing, host-only RPC forgery, early meetings.

**Meeting grace:**
- Blocks meetings called before a configurable grace period (default 10s) after each round starts. Catches modded clients that fire the meeting RPC directly to skip the emergency button.

**Modded lobby toggle:**
- One switch loosens the role/gameplay-semantic checks (venting, tasks, name length/format) so role mods don't trip false positives. The offensive-name, spam, and crash checks stay active.

**Host tools:**
- **Map hotswap** — spawn/despawn any map (Skeld, MiraHQ, Polus, Dleks, Airship, Fungle) live.
- **Lobby spawn/despawn.**
- **Force crew / impostor victory.**

## Safety design (the important part)

- **You are exempt from the anti-cheat.** Your own RPCs are never inspected, flagged, blocked, or punished (`player.AmOwner` check runs before everything). Using a feature or firing an action can never kick or ban you.
- **Nothing happens when you're not host.** Detection still runs and still notifies you of cheaters, but no RPC is discarded and no one is kicked/banned. In the panel, every control is grayed out and can't be toggled while you're not host — so a mistoggle can't get you kicked by your own tool.
- **Kicking/banning is host-only** and gated twice (dispatcher + `Flag`).


## Install
Copy `WellsAntiCheat.dll` into `<Among Us>/BepInEx/plugins/` and launch.

## Use
- **F8** opens/closes the panel (configurable).
- Host a lobby; the panel goes active. Set punishment (None / Kick / Ban), thresholds, and host tools.
- Edit the name blocklist at `<Among Us>/BepInEx/config/WellsAntiCheat_blocklist.txt` (one term per line, plain lowercase, leetspeak handled automatically). Click **Reload blocklist file** after editing.
- Settings persist in `<Among Us>/BepInEx/config/com.wells.anticheat.cfg`.

## Notes & limits
- Detection is heuristic: it raises the cost of cheating a lot, but a mod that only does things indistinguishable from legit play won't be caught.
- **Map hotswap is the least-tested feature.** Spawning/despawning ShipStatus mid-game can desync clients in edge cases; use it deliberately. The spawn/despawn calls themselves match the game's own flow.
- On a game update, if the build errors the likely culprits are the Harmony targets (`HandleRpc`, `ShipStatus.FixedUpdate`, `MeetingHud.Close`) or an `RpcCalls` member. Update the game libs to match.
- Flood/spam thresholds are tunable in the config if you see false positives during heavy legit bursts.
