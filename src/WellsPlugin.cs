using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace WellsAntiCheat
{
    [BepInPlugin(Guid, "Wells Anti Cheat", "1.0.0")]
    [BepInProcess("Among Us.exe")]
    public class WellsPlugin : BasePlugin
    {
        public const string Guid = "com.wells.anticheat";

        public new static ManualLogSource Log;
        private readonly Harmony _harmony = new(Guid);

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo("Wells Anti Cheat loading...");

            // Persistent settings (BepInEx/config/com.wells.anticheat.cfg).
            var antiCheat     = Config.Bind("General", "AntiCheatEnabled", true, "Master switch for the RPC anti-cheat.");
            var crash         = Config.Bind("General", "CrashProtection", true, "Detect malformed/flooded RPCs (crash attempts).");
            var nameFilter    = Config.Bind("General", "NameFilterEnabled", true, "Kick players with offensive/banned usernames.");
            var meetingTimer  = Config.Bind("General", "MeetingTimerEnabled", true, "Punish meetings called before the grace window.");
            var moddedLobby   = Config.Bind("General", "ModdedLobby", false, "Loosen role/gameplay checks for modded lobbies.");
            var discardRpc    = Config.Bind("General", "DiscardRpc", true, "Drop the offending RPC so its effect never applies.");
            var punishment    = Config.Bind("General", "Punishment", "Kick", "Action on a flagged player: None, Kick, or Ban.");
            var keyCfg        = Config.Bind("General", "ToggleKey", "F8", "Key to open/close the panel.");
            var graceCfg      = Config.Bind("Meeting", "GraceSeconds", 10f, "Seconds after a round starts before meetings are allowed.");
            var emergencyOnly = Config.Bind("Meeting", "EmergencyOnly", false, "Restrict only emergency-button meetings (body reports always allowed).");
            var floodCfg      = Config.Bind("Crash", "FloodThreshold", 50, "RPCs from one player within the flood window that trigger a crash-attempt flag.");
            var floodWindow   = Config.Bind("Crash", "FloodWindowSeconds", 1.0f, "Sliding window for the flood threshold.");

            Anticheat.Enabled          = antiCheat.Value;
            Anticheat.CrashProtection  = crash.Value;
            Anticheat.ModdedLobby      = moddedLobby.Value;
            Anticheat.DiscardRpc       = discardRpc.Value;
            Anticheat.Punishment       = ParsePunishment(punishment.Value);
            Anticheat.FloodThreshold   = floodCfg.Value;
            Anticheat.FloodWindow      = floodWindow.Value;
            NameFilter.Enabled         = nameFilter.Value;
            MeetingTimer.Enabled       = meetingTimer.Value;
            MeetingTimer.GraceSeconds  = graceCfg.Value;
            MeetingTimer.EmergencyOnly = emergencyOnly.Value;

            if (System.Enum.TryParse<KeyCode>(keyCfg.Value, true, out var key))
                WellsGui.ToggleKey = key;

            NameFilter.Load();

            _harmony.PatchAll();

            AddComponent<WellsGui>();

            Log.LogInfo("Wells Anti Cheat loaded. Press F8 in-game to open the panel.");
        }

        private static Anticheat.Punishments ParsePunishment(string s)
            => System.Enum.TryParse<Anticheat.Punishments>(s, true, out var p) ? p : Anticheat.Punishments.Kick;
    }
}
