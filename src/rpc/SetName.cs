using Hazel;

namespace WellsAntiCheat.Rpc
{
    // Fired when a name is actually committed. HyperMenu bails out entirely on modded lobbies
    // (`if (Anticheat.IsModded()) return;`), which is exactly where name-bypass tools live.
    // We keep the offensive-name check active in all lobby types and only relax the length/format
    // rules for modded lobbies (where custom long/colored names are legitimate).
    internal class SetName : RpcCheck
    {
        public const int MaxNameLength = 12; // +2 for the disambiguation numbers vanilla appends

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            reader.ReadUInt32(); // netId - not needed for our checks
            string requested = reader.ReadString();

            // Offensive-name check ALWAYS runs, modded or not.
            if (NameFilter.IsOffensive(requested, out var term))
            {
                blockRpc = true;
                Anticheat.Flag(player, $"'{requested}' contains a blocked term ('{term}').");
                return;
            }

            // On modded lobbies, long/colored names are expected; skip the cosmetic checks.
            if (Anticheat.IsModded()) return;

            if (requested.Length > MaxNameLength)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"'{requested}' is too long ({requested.Length}).");
                return;
            }

            if (requested.Contains('<'))
            {
                blockRpc = true;
                Anticheat.Flag(player, $"'{requested}' contains invalid formatting characters.");
            }
        }

        // On modded servers the host should never receive a SetName from a client.
        public override bool IsHostOnly() => Anticheat.IsModded();
    }
}
