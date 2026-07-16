using Hazel;

namespace WellsAntiCheat.Rpc
{
    // Meeting calls (emergency button and body reports) route through this RPC. We enforce the
    // grace-period rule here. This catches modded clients that fire the meeting RPC directly to
    // skip the emergency-button cooldown/limit (e.g. HyperMenu's "call meeting" button).
    internal class ReportDeadBody : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            // First byte is the reported player id; 0xFF (255) means an emergency-button meeting.
            byte targetId = reader.ReadByte();
            bool isEmergency = targetId == byte.MaxValue;

            // Meetings are never valid in Hide and Seek.
            if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek())
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} tried to call a meeting in Hide and Seek.");
                return;
            }

            // Grace-period rule.
            bool restricted = MeetingTimer.EmergencyOnly ? isEmergency : true;
            if (restricted && MeetingTimer.InGracePeriod(out float remaining))
            {
                blockRpc = true;
                string kind = isEmergency ? "an emergency meeting" : "a body report";
                Anticheat.Flag(player,
                    $"{Anticheat.Name(player)} called {kind} {remaining:0.0}s too early " +
                    $"(grace {MeetingTimer.GraceSeconds:0}s).");
            }
        }
    }
}
