using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using UnityEngine;

namespace WellsAntiCheat.Rpc
{
    internal static class RoleUtil
    {
        public static bool Alive(PlayerControl p) => p != null && p.Data != null && !p.Data.IsDead;
        public static bool Impostor(PlayerControl p) => p?.Data != null && RoleManager.IsImpostorRole(p.Data.RoleType);
        public static bool IsRole(PlayerControl p, RoleTypes r) => p?.Data != null && p.Data.RoleType == r;
        public static bool InVent(PlayerControl p) => p != null && (p.inVent || p.walkingToVent);
        public static bool InRange(PlayerControl a, PlayerControl b, float range)
            => a != null && b != null && Vector2.Distance(a.GetTruePosition(), b.GetTruePosition()) <= range;
    }

    // Murder validation: killer must be a living impostor, not in a vent, within kill range of a
    // living non-impostor target. Ported from BetterAmongUs's CheckMurderHandler (vanilla-role).
    internal class CheckMurder : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (Anticheat.IsModded()) return; // custom roles change kill rules
            PlayerControl target = reader.ReadNetObject<PlayerControl>();
            if (target == null) return;

            bool killerOk = RoleUtil.Alive(player) && RoleUtil.Impostor(player) && !RoleUtil.InVent(player)
                            && RoleUtil.InRange(player, target, 3f);
            bool targetOk = RoleUtil.Alive(target) && !RoleUtil.Impostor(target);

            if (!killerOk || !targetOk)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} sent an invalid kill on {Anticheat.Name(target)}.");
            }
        }
    }

    internal class MurderPlayer : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (Anticheat.IsModded()) return;
            PlayerControl target = reader.ReadNetObject<PlayerControl>();
            if (target == null) return;

            bool killerOk = RoleUtil.Alive(player) && RoleUtil.Impostor(player) && !RoleUtil.InVent(player);
            if (!killerOk)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} committed a murder they can't legally perform.");
            }
        }
    }

    internal class Shapeshift : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (Anticheat.IsModded()) return;
            if (!RoleUtil.IsRole(player, RoleTypes.Shapeshifter) || !RoleUtil.Alive(player))
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} shapeshifted without being a living Shapeshifter.");
            }
        }
    }

    internal class StartVanish : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (Anticheat.IsModded()) return;
            if (!RoleUtil.IsRole(player, RoleTypes.Phantom) || !RoleUtil.Alive(player))
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} vanished without being a living Phantom.");
            }
        }
    }

    internal class ProtectPlayer : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (Anticheat.IsModded()) return;
            if (!RoleUtil.IsRole(player, RoleTypes.GuardianAngel))
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} protected a player without being a Guardian Angel.");
            }
        }
    }
}
