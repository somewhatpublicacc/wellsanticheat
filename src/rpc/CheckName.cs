using Hazel;

namespace WellsAntiCheat.Rpc
{
    // Fired when a client proposes a name (join / rename). This is the earliest place to catch
    // an offensive name, before it's committed. Runs regardless of modded status, unlike HyperMenu.
    internal class CheckName : RpcCheck
    {
        public const int MaxNameLength = 10;

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            string requested = reader.ReadString();

            if (NameFilter.IsOffensive(requested, out var term))
            {
                blockRpc = true;
                Anticheat.Flag(player, $"'{requested}' contains a blocked term ('{term}').");
                return;
            }

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
    }
}
