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

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                _open = !_open;
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

            Header("Meeting grace");
            MeetingTimer.Enabled = GUILayout.Toggle(MeetingTimer.Enabled, " Block early meetings");
            MeetingTimer.EmergencyOnly = GUILayout.Toggle(MeetingTimer.EmergencyOnly, " Emergency button only");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Grace: {MeetingTimer.GraceSeconds:0}s", GUILayout.Width(80));
            MeetingTimer.GraceSeconds = Mathf.Round(GUILayout.HorizontalSlider(MeetingTimer.GraceSeconds, 0f, 30f));
            GUILayout.EndHorizontal();

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
