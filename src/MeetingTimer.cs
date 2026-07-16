using HarmonyLib;
using System;

namespace WellsAntiCheat
{
    // Tracks when the current task phase began, so we can reject meetings called too early.
    // A round "starts" when gameplay (re)begins: the first ShipStatus.FixedUpdate after leaving
    // the lobby (first round) and every MeetingHud.Close (subsequent rounds). Both target methods
    // are confirmed to exist in the current Among Us API.
    internal static class MeetingTimer
    {
        public static bool Enabled = true;

        // How long after a round starts before meetings are allowed.
        public static float GraceSeconds = 10f;

        // If true, only emergency-button meetings (bodyId == 0xFF) are restricted; body reports
        // are always allowed. If false, all meetings are restricted during the grace window.
        public static bool EmergencyOnly = false;

        private static DateTime _roundStartUtc = DateTime.MinValue;
        private static bool _latched; // have we marked the start of the current game's first round?

        public static void MarkRoundStart() => _roundStartUtc = DateTime.UtcNow;

        public static bool InGracePeriod(out float remaining)
        {
            remaining = 0f;
            if (!Enabled || _roundStartUtc == DateTime.MinValue) return false;

            float elapsed = (float)(DateTime.UtcNow - _roundStartUtc).TotalSeconds;
            if (elapsed < GraceSeconds)
            {
                remaining = GraceSeconds - elapsed;
                return true;
            }
            return false;
        }

        // First round: latch the start the first frame gameplay is running (lobby despawned).
        // Reset the latch when we're back in the lobby so the next game re-arms.
        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.FixedUpdate))]
        private static class OnShipFixedUpdate
        {
            private static void Postfix()
            {
                if (LobbyBehaviour.Instance != null)
                {
                    _latched = false;
                    return;
                }
                if (!_latched)
                {
                    MarkRoundStart();
                    _latched = true;
                }
            }
        }

        // Every subsequent round begins when a meeting closes.
        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
        private static class OnMeetingClose
        {
            private static void Postfix() => MarkRoundStart();
        }
    }
}
