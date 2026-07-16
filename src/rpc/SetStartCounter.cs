using Hazel;

namespace WellsAntiCheat.Rpc
{
    // Non-host clients may only send SetStartCounter with a value of -1. Anything else is an
    // attempt to force/spoof the lobby countdown.
    internal class SetStartCounter : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            reader.ReadPackedInt32(); // sequence id
            sbyte counter = reader.ReadSByte();

            if (player.OwnerId != AmongUsClient.Instance.HostId && counter != -1)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} spoofed the start counter ({counter}).");

                // Revert the fake countdown value.
                if (AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer != null)
                    PlayerControl.LocalPlayer.RpcSetStartCounter(-1);
            }
        }
    }
}
