# Nitro Anti Cheat - Well

A host-side moderation mod for Among Us. It protects your lobby, does **nothing** unless you are the host, and **never acts against you**.

## Features (every one has its own on/off toggle)

**Cheat-client detection** — kicks known hack clients on sight by fingerprinting the custom RPCs they broadcast: **SickoMenu**, **AmongUsMenu (AUM)**, and **KillNetwork** (plus their chat channels).

**Name filter** — leetspeak-aware. Includes `Antipride` plus a slur list; catches `n1gg3r`, `n i g g e r`, `niiigger`, etc. Editable blocklist file, no recompile.

**Crash / flood protection** (individually toggleable):
- malformed / too-short RPC payloads
- RPC flooding (tunable threshold)
- unregistered / unknown RPCs (auto-off in modded lobbies)
- oversized chat messages

**Chat spam** — kicks players sending too many messages in a short window.

**State checks** (individually toggleable):
- host-only RPCs sent by a non-host
- cosmetic changes during gameplay
- gameplay RPCs (murder, vent, vote, meeting, roles...) fired while in the lobby

**RPC anti-cheat** — illegal vents, teleports in lobby, impostors completing tasks, start-counter spoofing, early meetings, plus **role-exploit checks**: invalid kills, shapeshifting without being a Shapeshifter, vanishing without being a Phantom, protecting without being a Guardian Angel.

**Sabotage / system protection (from Hydra)** — validates `UpdateSystem`: blocks reactor force-fix and force-call crashes, mushroom-mixup spoofing, switch-panel crash payloads, sabotaging as a non-impostor, and sabotage during Hide and Seek. Also door-close in Hide and Seek.

**More RPC exploit checks (from Hydra)** — fake visual-task animations (`PlayAnimation`), the never-legitimate `Exiled` RPC, invalid colour / net-id (`SetColor`), fake medbay scans (`SetScanner`), airship platform misuse (`UsePlatform`), and level spoofing (`SetLevel`).

**Votekick abuse** — blocks votekicks from unknown clients, dead players, or outside a meeting.

**Banned players (notepad)** — an editable list in the panel (also `BepInEx/config/NitroAntiCheat_banned.txt`). One player name or friend code per line; matches are auto-kicked while you host. Editable even when you're not host.

**Auto-mute (meeting)** — optional, off by default. "Mute" = the host silently drops the player's chat RPCs for the rest of the meeting (Among Us has no native mute). Two triggers: mute a player once they hold a **majority of votes** (stops a dying impostor leaking), or mute the player of a **colour a majority of living players name in chat**.

**Exploit protection (self-defense, works even when you're not host)** — blocks known mass-grief attacks aimed at you:
- **Ventilation kick/ban exploit** — the "everyone gets banned by InnerSloth" attack (forged `UpdateSystem(Ventilation)` to non-host clients). Blocked.
- **Forced teleport / mass-vent** — forged `SnapTo` aimed at your own player. Blocked.
- **Voting-overload crash** — `VotingComplete` with a huge array length that forces gigabytes of allocation. Blocked.
- **Oversized messages** — game-data messages over 1400 bytes dropped.
Forgeable vent/movement/teleport checks are discard-only (they block the bad effect but never kick the apparent sender, so a forged mass-vent can't make Nitro kick the victims).

**Meeting grace** — blocks meetings called before a configurable window (default 10s) after each round starts. Catches modded clients that fire the meeting RPC directly.

**Modded lobby toggle** — one switch loosens role/gameplay-semantic checks so role mods don't false-positive; name, spam, cheat-client, and crash detection stay active.

**Host tools** — live map hotswap (Skeld/Mira/Polus/Dleks/Airship/Fungle), lobby spawn/despawn, force crew/impostor win.

**GUI** — press **F8**; the window frame cycles through colours (toggle "Rainbow GUI" off if you prefer). Green/orange status line shows host state.

## Safety design

- **You are exempt.** Your own RPCs are never inspected, flagged, blocked, or punished (`player.AmOwner` check runs before everything). Using any feature can't kick or ban you.
- **Nothing happens when you're not host.** Detection runs and notifies, but no RPC is discarded and nobody is kicked; every control is grayed out and won't toggle.
- **Kick/ban is host-only**, gated twice.

---

## Build (you compile it yourself)

IL2CPP BepInEx plugins must be built against your game's interop assemblies.

**Prerequisites:** .NET 6 SDK; BepInEx 6 (IL2CPP) installed in your Among Us folder (run the game once so interop assemblies generate).

> The three build packages live on the **BepInEx feed**, not nuget.org. The included `nuget.config` (next to the `.csproj`) points there. Build from inside the project folder.

1. Set `AmongUs.GameLibs.Steam` in `NitroAntiCheat.csproj` to your game version.
2. From the project folder: `dotnet build -c Release`
3. Output: `bin/Release/net6.0/NitroAntiCheat.dll` → copy into `<Among Us>/BepInEx/plugins/`.

## Use
- **F8** opens the panel. Host a lobby to activate it.
- Every check has its own toggle; set punishment (None/Kick/Ban) and thresholds.
- Name blocklist: `<Among Us>/BepInEx/config/NitroAntiCheat_blocklist.txt` (one term per line, plain lowercase). Click **Reload blocklist file** after editing.
- Settings persist in `<Among Us>/BepInEx/config/com.well.nitroanticheat.cfg`.

## Notes & limits
- Detection is heuristic; it raises the cost of cheating a lot but can't catch a mod that only does legit-looking things.
- **Map hotswap is the least-tested feature** — mid-game ShipStatus swaps can desync in edge cases.
- Cheat-client fingerprints (Sicko/AUM/KillNetwork) match the RPC IDs those clients used as of this build; a client that changes its IDs would evade that specific check (the behavioural checks still apply).
- On a game update, if the build errors the likely culprits are the Harmony targets or an `RpcCalls`/`RoleTypes` member. Update the game libs.
