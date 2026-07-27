using HarmonyLib;
using Hazel;
using InnerNet;

namespace WellsAntiCheat
{
    // Self-protection against known mass-grief exploits. Unlike the main anticheat (which only
    // discards RPCs on the host), these block dangerous INCOMING RPCs on ANY client, so they
    // protect you from being kicked / banned / crashed even when you are not hosting.
    internal static class Protections
    {
        public static bool BlockVentKickExploit = true; // "everyone gets banned" (Ventilation exploit)
        public static bool BlockServerTeleports = true; // mass teleport-to-vent (SnapTo on us)
        public static bool BlockVotingOverload  = true; // VotingComplete out-of-memory crash
        public static bool BlockLargeMessages   = true; // oversized game-data message attack
        public static int  MaxMessageLength     = 1400;

        private static bool AmHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

        // Called at the very top of the RPC dispatcher (before the anticheat toggle / exemption),
        // for the net objects the dispatcher already hooks. Returns true if the RPC must be blocked.
        public static bool ShouldBlock(System.Type netObj, PlayerControl player, byte callId, MessageReader reader)
        {
            var rpc = (RpcCalls)callId;

            // Mass teleport-to-vent: on vanilla servers you never legitimately RECEIVE a SnapTo for
            // your own player (your movement is client-authoritative), so block it.
            if (BlockServerTeleports && netObj == typeof(CustomNetworkTransform)
                && rpc == RpcCalls.SnapTo && player != null && player == PlayerControl.LocalPlayer)
            {
                Notifier.Show("Blocked a forced teleport aimed at you.");
                return true;
            }

            // Ventilation kick/ban exploit: a hacker sends UpdateSystem(Ventilation) to a non-host
            // client to trip InnerSloth's anticheat into banning them. Block it when not host.
            if (BlockVentKickExploit && netObj == typeof(ShipStatus)
                && rpc == RpcCalls.UpdateSystem && !AmHost)
            {
                int pos = reader.Position;
                try
                {
                    var system = (SystemTypes)reader.ReadByte();
                    reader.Position = pos;
                    if (system == SystemTypes.Ventilation)
                    {
                        Notifier.Show("Blocked a ventilation kick/ban exploit aimed at you.");
                        return true;
                    }
                }
                catch { reader.Position = pos; }
            }

            return false;
        }

        // VotingComplete with a huge array length forces the client to allocate gigabytes -> crash.
        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.HandleRpc))]
        private static class VotingOverloadGuard
        {
            private static bool Prefix(byte callId, MessageReader reader)
            {
                if (!BlockVotingOverload || callId != (byte)RpcCalls.VotingComplete) return true;

                int pos = reader.Position;
                try
                {
                    int arrayLength = reader.ReadPackedInt32();
                    if (arrayLength > 1024 || arrayLength > reader.BytesRemaining)
                    {
                        Notifier.Show("Blocked a voting-overload crash attempt.");
                        return false;
                    }
                    reader.Position = pos;
                }
                catch { reader.Position = pos; }
                return true;
            }
        }

        // Drop oversized game-data messages (large-message crash / mass-ban vector).
        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleGameData))]
        private static class LargeMessageGuard
        {
            private static bool Prefix(MessageReader parentReader)
            {
                if (!BlockLargeMessages) return true;
                if (parentReader != null && parentReader.Length > MaxMessageLength)
                {
                    parentReader.Recycle();
                    return false;
                }
                return true;
            }
        }
    }
}
