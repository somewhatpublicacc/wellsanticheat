using Hazel;
using InnerNet;
using System;

namespace WellsAntiCheat.Rpc
{
    // Validates sabotage / system-update RPCs. This is the big one: it stops reactor force-fix
    // and force-call crashes, mushroom-mixup spoofing, switch-panel crash payloads, sabotaging as
    // a non-impostor, and sabotage in Hide and Seek. Ported from Hydra's UpdateSystem.
    // Arrives on the ShipStatus net object; the acting player is read from the payload.
    internal class UpdateSystem : RpcCheck
    {
        private static readonly SystemTypes[] UpdatableWhenDead =
        {
            SystemTypes.MedBay, SystemTypes.Sabotage, SystemTypes.Security, SystemTypes.Ventilation
        };

        public override Type GetExpectedNetObject() => typeof(ShipStatus);

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            SystemTypes system = (SystemTypes)reader.ReadByte();
            player = reader.ReadNetObject<PlayerControl>();
            if (player == null) return;
            if (Anticheat.IsExempt(player)) return; // local + trusted exempt

            if (ShipStatus.Instance == null || !ShipStatus.Instance.Systems.ContainsKey(system))
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} updated system {system} that this map doesn't have.");
                blockRpc = true;
                return;
            }

            if (player.Data.IsDead && Array.IndexOf(UpdatableWhenDead, system) < 0)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} updated system {system} while dead.");
                blockRpc = true;
                return;
            }

            switch (system)
            {
                case SystemTypes.Electrical:  ValidateSwitches(player, reader, ref blockRpc); break;
                case SystemTypes.MushroomMixupSabotage: ValidateMushroom(player, ref blockRpc); break;
                case SystemTypes.Reactor:
                case SystemTypes.Laboratory:
                case SystemTypes.HeliSabotage: ValidateReactor(player, reader, ref blockRpc); break;
                case SystemTypes.Sabotage:     ValidateSabotage(player, reader, ref blockRpc); break;
            }
        }

        // Mushroom Mixup is host-only in the game logic; a player sending it is always invalid.
        private static void ValidateMushroom(PlayerControl player, ref bool blockRpc)
        {
            Anticheat.Flag(player, $"{Anticheat.Name(player)} tried to force a Mushroom Mixup sabotage.");
            blockRpc = true;
        }

        private static void ValidateReactor(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            byte op = reader.ReadByte();
            if (op == 16)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} tried to force-fix a Reactor sabotage.");
                blockRpc = true;
            }
            else if (op == 128)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} tried to force-call a Reactor sabotage.");
                blockRpc = true;
            }
        }

        private static void ValidateSabotage(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            SystemTypes target = (SystemTypes)reader.ReadByte();

            if (!MapUtil.ValidSabotages.Contains(target))
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} tried to sabotage an invalid system: {target}.");
                blockRpc = true;
            }
            if (player.Data != null && !RoleManager.IsImpostorRole(player.Data.RoleType))
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} tried to sabotage {target} without being an impostor.");
                blockRpc = true;
            }
            if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek())
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} tried to sabotage {target} in Hide and Seek.");
                blockRpc = true;
            }
        }

        private static void ValidateSwitches(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            byte switches = reader.ReadByte();

            if ((switches & 128) != 0)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} sent a bulk-switch crash payload ({switches}).");
                blockRpc = true;
            }
            else if (switches > 5)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} toggled an invalid switch ({switches}).");
                blockRpc = true;
            }

            // Silently block switch updates when lights aren't sabotaged or during a meeting -
            // these can false-flag on race conditions, so we block without punishing.
            try
            {
                var sys = ShipStatus.Instance.Systems[SystemTypes.Electrical].Cast<SwitchSystem>();
                if (sys != null && sys.ExpectedSwitches == sys.ActualSwitches)
                    blockRpc = true;
            }
            catch { }

            if (MeetingHud.Instance != null)
                blockRpc = true;
        }
    }

    // Doors can't be closed during Hide and Seek. Sender is unknown (routes through ShipStatus).
    internal class CloseDoorsOfType : RpcCheck
    {
        public override Type GetExpectedNetObject() => typeof(ShipStatus);

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek())
            {
                Anticheat.Flag("Someone tried to close doors during Hide and Seek.");
                blockRpc = true;
            }
        }
    }
}
