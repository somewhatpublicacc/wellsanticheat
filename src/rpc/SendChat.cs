using Hazel;
using UnityEngine;

namespace WellsAntiCheat.Rpc
{
    // Detects chat spam (too many messages in a short window) and oversized messages that can
    // lag or crash clients.
    internal class SendChat : RpcCheck
    {
        public static int SpamThreshold = 5;      // messages within SpamWindow => spam
        public static float SpamWindow = 3.0f;    // seconds
        public static int MaxMessageLength = 300; // longer than this is treated as a crash attempt

        private static readonly RateTracker _chatRate = new();

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (player == null) return;

            string message = reader.ReadString();

            if (message != null && message.Length > MaxMessageLength)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} sent an oversized chat message ({message.Length} chars) - crash attempt.");
                return;
            }

            int count = _chatRate.Record(player.OwnerId, Time.realtimeSinceStartup, SpamWindow);
            if (count > SpamThreshold)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} is spamming chat ({count} msgs / {SpamWindow:0.#}s).");
            }
        }
    }
}
