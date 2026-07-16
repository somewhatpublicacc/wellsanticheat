using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace WellsAntiCheat
{
    [BepInPlugin(Guid, "Nitro Anti Cheat - Well", "1.1.0")]
    [BepInProcess("Among Us.exe")]
    public class WellsPlugin : BasePlugin
    {
        public const string Guid = "com.well.nitroanticheat";

        public new static ManualLogSource Log;
        private readonly Harmony _harmony = new(Guid);

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo("Nitro Anti Cheat - Well loading...");

            // Persistent settings (BepInEx/config/com.well.nitroanticheat.cfg).
            var antiCheat     = Config.Bind("General", "AntiCheatEnabled", true, "Master switch for the RPC anti-cheat.");
            var cheatClients  = Config.Bind("General", "DetectCheatClients", true, "Kick known cheat clients (SickoMenu/AUM/KillNetwork).");
            var nameFilter    = Config.Bind("General", "NameFilterEnabled", true, "Kick players with offensive/banned usernames.");
            var meetingTimer  = Config.Bind("General", "MeetingTimerEnabled", true, "Punish meetings called before the grace window.");
            var moddedLobby   = Config.Bind("General", "ModdedLobby", false, "Loosen role/gameplay checks for modded lobbies.");
            var discardRpc    = Config.Bind("General", "DiscardRpc", true, "Drop the offending RPC so its effect never applies.");
            var punishment    = Config.Bind("General", "Punishment", "Kick", "Action on a flagged player: None, Kick, or Ban.");
            var keyCfg        = Config.Bind("General", "ToggleKey", "F8", "Key to open/close the panel.");
            var rainbow       = Config.Bind("General", "RainbowGui", true, "Cycle the GUI colour.");

            var crash         = Config.Bind("Crash", "CrashProtection", true, "Master switch for crash/flood protection.");
            var malformed     = Config.Bind("Crash", "CheckMalformed", true, "Flag malformed (too-short) RPC payloads.");
            var flood         = Config.Bind("Crash", "CheckFlood", true, "Flag RPC flooding.");
            var unknownRpc    = Config.Bind("Crash", "DetectUnknownRpc", true, "Flag unregistered/unknown RPCs (auto-off in modded lobbies).");
            var floodCfg      = Config.Bind("Crash", "FloodThreshold", 50, "RPCs within the window that trigger a flood flag.");
            var floodWindow   = Config.Bind("Crash", "FloodWindowSeconds", 1.0f, "Sliding window for the flood threshold.");

            var stateMaster   = Config.Bind("State", "StateChecks", true, "Master switch for state-based RPC rejection.");
            var stCosmetics   = Config.Bind("State", "CheckCosmetics", true, "Reject cosmetic changes during gameplay.");
            var stLobby       = Config.Bind("State", "CheckLobbyRpcs", true, "Reject gameplay RPCs sent while in the lobby.");

            var graceCfg      = Config.Bind("Meeting", "GraceSeconds", 10f, "Seconds after a round starts before meetings are allowed.");
            var emergencyOnly = Config.Bind("Meeting", "EmergencyOnly", false, "Restrict only emergency-button meetings.");

            Anticheat.Enabled          = antiCheat.Value;
            Anticheat.CrashProtection  = crash.Value;
            Anticheat.CheckMalformed   = malformed.Value;
            Anticheat.CheckFlood       = flood.Value;
            Anticheat.DetectUnknownRpc = unknownRpc.Value;
            Anticheat.ModdedLobby      = moddedLobby.Value;
            Anticheat.DiscardRpc       = discardRpc.Value;
            Anticheat.Punishment       = ParsePunishment(punishment.Value);
            Anticheat.FloodThreshold   = floodCfg.Value;
            Anticheat.FloodWindow      = floodWindow.Value;
            CheatClients.Enabled       = cheatClients.Value;
            StateChecks.Enabled        = stateMaster.Value;
            StateChecks.CheckCosmetics = stCosmetics.Value;
            StateChecks.CheckLobbyRpcs = stLobby.Value;
            NameFilter.Enabled         = nameFilter.Value;
            MeetingTimer.Enabled       = meetingTimer.Value;
            MeetingTimer.GraceSeconds  = graceCfg.Value;
            MeetingTimer.EmergencyOnly = emergencyOnly.Value;
            WellsGui.RainbowGui        = rainbow.Value;

            if (System.Enum.TryParse<KeyCode>(keyCfg.Value, true, out var key))
                WellsGui.ToggleKey = key;

            NameFilter.Load();
            _harmony.PatchAll();
            AddComponent<WellsGui>();

            Log.LogInfo("Nitro Anti Cheat - Well loaded. Press F8 in-game to open the panel.");
        }

        private static Anticheat.Punishments ParsePunishment(string s)
            => System.Enum.TryParse<Anticheat.Punishments>(s, true, out var p) ? p : Anticheat.Punishments.Kick;
    }
}
