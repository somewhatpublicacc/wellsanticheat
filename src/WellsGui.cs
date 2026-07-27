using System;
using UnityEngine;

namespace WellsAntiCheat
{
    // Injected MonoBehaviour that draws the panel (default key F8). When you are NOT host every
    // control is grayed out and cannot be changed - detection still runs and still notifies you
    // of cheaters, it simply takes no action. The window frame cycles through colours.
    public class WellsGui : MonoBehaviour
    {
        public WellsGui(IntPtr ptr) : base(ptr) { }

        public const string Title = "Nitro Anti Cheat - Well";
        public static KeyCode ToggleKey = KeyCode.F8;
        public static bool RainbowGui = true;

        private bool _open = true;
        private Rect _window = new Rect(20, 20, 350, 720);
        private Vector2 _scroll = Vector2.zero;
        private const int WindowId = 0x4E495452; // "NITR"

        private float _banTimer;
        private bool _loggedSelf;

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                _open = !_open;

            LogSelfOnce();
            EnforceBans();
            MuteManager.CheckMajorityVotes();
        }

        // Prints your own friend code + name to the BepInEx console once, so you can paste the
        // exact value into the hardcoded trusted list in TrustedPlayers.cs. Only visible in your log.
        private void LogSelfOnce()
        {
            if (_loggedSelf) return;
            var me = PlayerControl.LocalPlayer;
            if (me == null || me.Data == null) return;
            _loggedSelf = true;
            WellsPlugin.Log.LogInfo($"[Nitro] Your identifiers -> FriendCode: '{me.Data.FriendCode}'  Name: '{me.Data.PlayerName}'");
        }

        // Host-only: periodically kick anyone on the banned list. Never touches the local player.
        private void EnforceBans()
        {
            if (!BannedPlayers.Enabled) return;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

            _banTimer += Time.deltaTime;
            if (_banTimer < 2f) return;
            _banTimer = 0f;

            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p == null || Anticheat.IsExempt(p)) continue; // never ban self or trusted users
                if (BannedPlayers.IsBanned(p))
                {
                    Anticheat.Flag(p, $"{Anticheat.Name(p)} is on the banned list.", false);
                    AmongUsClient.Instance.KickPlayer(p.OwnerId, true);
                }
            }
        }

        // Current cycling accent colour.
        private static Color Accent()
            => RainbowGui ? Color.HSVToRGB(Mathf.Repeat(Time.time * 0.15f, 1f), 0.65f, 1f) : Color.white;

        private void OnGUI()
        {
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            DrawNotifications();

            if (!_open) return;

            // Tint the whole window frame with the cycling accent so the GUI visibly changes colour.
            GUI.backgroundColor = Accent();
            _window = GUI.Window(WindowId, _window, (GUI.WindowFunction)DrawWindow, Title);
            GUI.backgroundColor = Color.white;
        }

        private void Header(string text)
        {
            GUI.contentColor = Accent();
            GUILayout.Label(text);
            GUI.contentColor = Color.white;
        }

        private void DrawWindow(int id)
        {
            bool amHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

            _scroll = GUILayout.BeginScrollView(_scroll);

            GUI.contentColor = amHost ? Color.green : new Color(1f, 0.6f, 0.1f);
            GUILayout.Label(amHost ? "Status: HOST - fully active"
                                   : "Status: NOT HOST - alerts only, controls locked");
            GUI.contentColor = Color.white;

            // Interactive only while hosting.
            GUI.enabled = amHost;

            Header("Lobby");
            Anticheat.ModdedLobby = GUILayout.Toggle(Anticheat.ModdedLobby, " Modded lobby (loosen role checks)");

            Header("Detection (master toggles)");
            Anticheat.Enabled = GUILayout.Toggle(Anticheat.Enabled, " Anti-cheat master switch");
            CheatClients.Enabled = GUILayout.Toggle(CheatClients.Enabled, " Detect cheat clients (Sicko/AUM/KN)");
            NameFilter.Enabled = GUILayout.Toggle(NameFilter.Enabled, " Kick offensive / banned names");
            if (GUILayout.Button("Reload blocklist file")) NameFilter.Load();

            Header("Crash / flood");
            Anticheat.CrashProtection = GUILayout.Toggle(Anticheat.CrashProtection, " Crash protection (master)");
            Anticheat.CheckMalformed = GUILayout.Toggle(Anticheat.CheckMalformed, "   - malformed RPC payloads");
            Anticheat.CheckFlood = GUILayout.Toggle(Anticheat.CheckFlood, "   - RPC flooding");
            Anticheat.DetectUnknownRpc = GUILayout.Toggle(Anticheat.DetectUnknownRpc, " Unregistered/unknown RPCs");

            Header("State checks");
            StateChecks.Enabled = GUILayout.Toggle(StateChecks.Enabled, " State checks (master)");
            StateChecks.CheckCosmetics = GUILayout.Toggle(StateChecks.CheckCosmetics, "   - cosmetic change mid-game");
            StateChecks.CheckLobbyRpcs = GUILayout.Toggle(StateChecks.CheckLobbyRpcs, "   - gameplay RPC in lobby");

            Header("Chat");
            var chat = Anticheat.RpcHandlers.TryGetValue(RpcCalls.SendChat, out var sc) ? sc : null;
            if (chat != null) chat.Enabled = GUILayout.Toggle(chat.Enabled, " Chat spam / oversized message");
            WellsAntiCheat.Rpc.VoteKickGuard.Enabled = GUILayout.Toggle(WellsAntiCheat.Rpc.VoteKickGuard.Enabled, " Votekick abuse (dead / no-meeting)");

            Header("Meeting grace");
            MeetingTimer.Enabled = GUILayout.Toggle(MeetingTimer.Enabled, " Block early meetings");
            MeetingTimer.EmergencyOnly = GUILayout.Toggle(MeetingTimer.EmergencyOnly, " Emergency button only");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Grace: {MeetingTimer.GraceSeconds:0}s", GUILayout.Width(80));
            MeetingTimer.GraceSeconds = Mathf.Round(GUILayout.HorizontalSlider(MeetingTimer.GraceSeconds, 0f, 30f));
            GUILayout.EndHorizontal();

            Header("Exploit protection (self-defense)");
            Protections.BlockVentKickExploit = GUILayout.Toggle(Protections.BlockVentKickExploit, " Block ventilation kick/ban exploit");
            Protections.BlockServerTeleports = GUILayout.Toggle(Protections.BlockServerTeleports, " Block forced teleports (mass vent)");
            Protections.BlockVotingOverload = GUILayout.Toggle(Protections.BlockVotingOverload, " Block voting-overload crash");
            Protections.BlockLargeMessages = GUILayout.Toggle(Protections.BlockLargeMessages, " Block oversized messages");

            Header("Auto-mute (meeting)");
            MuteManager.MuteOnMajorityVote = GUILayout.Toggle(MuteManager.MuteOnMajorityVote, " Mute player with majority votes");
            MuteManager.MuteOnChatConsensus = GUILayout.Toggle(MuteManager.MuteOnChatConsensus, " Mute by chat colour vote");

            Header("On violation");
            Anticheat.SendNotification = GUILayout.Toggle(Anticheat.SendNotification, " Notify");
            Anticheat.DiscardRpc = GUILayout.Toggle(Anticheat.DiscardRpc, " Discard the RPC");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Punish: {Anticheat.Punishment}", GUILayout.Width(110));
            Anticheat.Punishment = (Anticheat.Punishments)Mathf.RoundToInt(
                GUILayout.HorizontalSlider((float)Anticheat.Punishment, 0, 2));
            GUILayout.EndHorizontal();

            Header("Individual RPC checks");
            foreach (var kvp in Anticheat.RpcHandlers)
                kvp.Value.Enabled = GUILayout.Toggle(kvp.Value.Enabled, $" {kvp.Key}");

            Header("Appearance");
            RainbowGui = GUILayout.Toggle(RainbowGui, " Rainbow GUI");

            // Banned-players editor. In-game text fields crash on this stripped IL2CPP build
            // (GUI.DoTextField can't be unstripped), so we use buttons instead. For bulk edits,
            // edit BepInEx/config/NitroAntiCheat_banned.txt in a real text editor and hit Reload.
            bool prevEnabled = GUI.enabled;
            GUI.enabled = true;
            Header("Ban a player in this lobby");
            BannedPlayers.Enabled = GUILayout.Toggle(BannedPlayers.Enabled, " Auto-kick banned players (host only)");
            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.AmOwner) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(Anticheat.Name(p), GUILayout.Width(150));
                if (GUILayout.Button("Ban", GUILayout.Width(70)))
                {
                    var code = p.Data.FriendCode ?? "";
                    BannedPlayers.Add(code.Length > 0 ? code : p.Data.PlayerName);
                }
                GUILayout.EndHorizontal();
            }

            Header("Banned entries");
            foreach (var entry in BannedPlayers.EntryList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry, GUILayout.Width(150));
                if (GUILayout.Button("Unban", GUILayout.Width(70)))
                    BannedPlayers.Remove(entry);
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Reload file")) BannedPlayers.Load();
            GUI.enabled = prevEnabled;

            Header("Host tools");
            GUILayout.Label($"Map: {(MapNames)HostTools.SelectedMap}");
            HostTools.SelectedMap = (byte)Mathf.Round(
                GUILayout.HorizontalSlider(HostTools.SelectedMap, 0, HostTools.MaxMapId));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Map")) HostTools.SpawnMap();
            if (GUILayout.Button("Despawn Map")) HostTools.DespawnMap();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Lobby")) HostTools.SpawnLobby();
            if (GUILayout.Button("Despawn Lobby")) HostTools.DespawnLobby();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Crew Win")) HostTools.ForceCrewVictory();
            if (GUILayout.Button("Force Impostor Win")) HostTools.ForceImpostorVictory();
            GUILayout.EndHorizontal();

            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void DrawNotifications()
        {
            float y = 10f;
            foreach (var msg in Notifier.Recent())
            {
                GUI.Box(new Rect(Screen.width - 420, y, 410, 40), msg);
                y += 44f;
            }
        }
    }
}
