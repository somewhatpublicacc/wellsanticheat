using System;
using UnityEngine;

namespace WellsAntiCheat
{
    // Injected MonoBehaviour that draws the panel. Toggle with the configured key (default F8).
    // When you are NOT host, every control is grayed out and cannot be changed - but detection
    // still runs and still notifies you of cheaters; it simply takes no action.
    public class WellsGui : MonoBehaviour
    {
        public WellsGui(IntPtr ptr) : base(ptr) { }

        public static KeyCode ToggleKey = KeyCode.F8;

        private bool _open = true;
        private Rect _window = new Rect(20, 20, 340, 620);
        private Vector2 _scroll = Vector2.zero;
        private const int WindowId = 0x57454C; // "WEL"

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                _open = !_open;
        }

        private void OnGUI()
        {
            GUI.enabled = true;          // notifications are never grayed
            DrawNotifications();

            if (!_open) return;
            _window = GUI.Window(WindowId, _window, (GUI.WindowFunction)DrawWindow, "Wells Anti Cheat");
        }

        private void DrawWindow(int id)
        {
            bool amHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label(amHost
                ? "Status: HOST - fully active"
                : "Status: NOT HOST - alerts only, controls locked");

            // Everything below is interactive only while hosting.
            GUI.enabled = amHost;

            GUILayout.Space(6);
            GUILayout.Label("Lobby:");
            Anticheat.ModdedLobby = GUILayout.Toggle(Anticheat.ModdedLobby, " Modded lobby (loosen checks for role mods)");

            GUILayout.Space(6);
            GUILayout.Label("Detection:");
            Anticheat.Enabled = GUILayout.Toggle(Anticheat.Enabled, " Anti-cheat (RPC checks)");
            Anticheat.CrashProtection = GUILayout.Toggle(Anticheat.CrashProtection, " Crash / flood protection");
            NameFilter.Enabled = GUILayout.Toggle(NameFilter.Enabled, " Kick offensive / banned names");
            if (GUILayout.Button("Reload blocklist file"))
                NameFilter.Load();

            GUILayout.Space(6);
            GUILayout.Label("Meeting grace:");
            MeetingTimer.Enabled = GUILayout.Toggle(MeetingTimer.Enabled, " Block early meetings");
            MeetingTimer.EmergencyOnly = GUILayout.Toggle(MeetingTimer.EmergencyOnly, " Emergency button only");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Grace: {MeetingTimer.GraceSeconds:0}s", GUILayout.Width(80));
            MeetingTimer.GraceSeconds = Mathf.Round(GUILayout.HorizontalSlider(MeetingTimer.GraceSeconds, 0f, 30f));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("On violation:");
            Anticheat.SendNotification = GUILayout.Toggle(Anticheat.SendNotification, " Notify");
            Anticheat.DiscardRpc = GUILayout.Toggle(Anticheat.DiscardRpc, " Discard the RPC");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Punish: {Anticheat.Punishment}", GUILayout.Width(110));
            Anticheat.Punishment = (Anticheat.Punishments)Mathf.RoundToInt(
                GUILayout.HorizontalSlider((float)Anticheat.Punishment, 0, 2));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Individual RPC checks:");
            foreach (var kvp in Anticheat.RpcHandlers)
                kvp.Value.Enabled = GUILayout.Toggle(kvp.Value.Enabled, $" {kvp.Key}");

            GUILayout.Space(8);
            GUILayout.Label("--- Host tools ---");

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
