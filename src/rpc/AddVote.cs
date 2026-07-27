using HarmonyLib;

namespace WellsAntiCheat.Rpc
{
    // Votekick validation. Patches VoteBanSystem.AddVote directly (the method every reference
    // client patches - VoteBanSystem.HandleRpc is not reliably patchable). Runs host-side and
    // blocks votekicks from unknown clients, dead players, or outside a meeting.
    [HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.AddVote))]
    internal static class VoteKickGuard
    {
        public static bool Enabled = true;

        private static bool Prefix(int srcClient, int clientId)
        {
            if (!Anticheat.Enabled || !Enabled) return true;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

            var client = AmongUsClient.Instance.FindClientById(srcClient);
            if (client == null || client.Character == null)
            {
                Anticheat.Flag($"An unknown client ({srcClient}) tried to votekick someone.");
                return false; // block
            }

            var voter = client.Character;
            if (Anticheat.IsExempt(voter)) return true; // local + trusted exempt

            if (voter.Data != null && voter.Data.IsDead)
            {
                Anticheat.Flag(voter, $"{Anticheat.Name(voter)} tried to votekick while dead.");
                return false;
            }

            if (MeetingHud.Instance == null)
            {
                Anticheat.Flag(voter, $"{Anticheat.Name(voter)} tried to votekick outside a meeting.");
                return false;
            }

            return true;
        }
    }
}
