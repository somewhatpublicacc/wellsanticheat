using Hazel;
using System.Collections.Generic;

namespace WellsAntiCheat
{
    // Detects known cheat clients by the custom RPC IDs they broadcast. These IDs live outside
    // the vanilla RpcCalls range, so a normal client never sends them. Fingerprints and the
    // detection specifics (empty payload for Sicko, self-id byte for AUM) are taken from
    // BetterAmongUs's cheat-client handlers.
    internal static class CheatClients
    {
        public static bool Enabled = true;

        // Cheat-client custom RPC IDs, reduced to their byte value (how they arrive on the wire).
        private const byte Sicko           = 164; // CustomRPC 420 -> byte 164, empty payload
        private const byte AUM             = 85;  // CustomRPC 42069 -> byte 85, first byte == sender PlayerId
        private const byte AUMChat         = 101;
        private const byte KillNetwork     = 250;
        private const byte KillNetworkChat = 119;

        // Players already identified, so we don't spam notifications every packet.
        private static readonly HashSet<int> _known = new();

        // Returns true if this RPC identifies a cheat client (caller should block + the player is
        // flagged/punished here).
        public static bool Check(PlayerControl player, byte callId, MessageReader reader)
        {
            if (!Enabled || player == null) return false;

            string client = null;

            switch (callId)
            {
                case Sicko:
                    if (reader.BytesRemaining == 0) client = "SickoMenu";
                    break;

                case AUM:
                    // AUM's fingerprint: the payload's first byte is the sender's own PlayerId.
                    if (reader.BytesRemaining >= 1)
                    {
                        int savedPos = reader.Position;
                        byte id = reader.ReadByte();
                        reader.Position = savedPos;
                        if (id == player.PlayerId) client = "AmongUsMenu (AUM)";
                    }
                    break;

                case AUMChat:        client = "AmongUsMenu (AUM) chat"; break;
                case KillNetwork:    client = "KillNetwork"; break;
                case KillNetworkChat: client = "KillNetwork chat"; break;
            }

            if (client == null) return false;

            // Only notify/punish once per player, but always report a match so the caller blocks.
            if (_known.Add(player.OwnerId))
                Anticheat.Flag(player, $"{Anticheat.Name(player)} is running {client} (cheat client detected).");

            return true;
        }

        public static void ResetKnown() => _known.Clear();
    }
}
