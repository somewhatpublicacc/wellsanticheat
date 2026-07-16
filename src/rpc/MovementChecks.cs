using Hazel;
using System;
using UnityEngine;

namespace WellsAntiCheat.Rpc
{
    // Task completion: impossible-context and impostor-completing-tasks detection.
    internal class CompleteTask : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            uint taskIndex = reader.ReadPackedUInt32();

            if (ShipStatus.Instance == null)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} completed task {taskIndex} with no ShipStatus.");
                return;
            }

            // Role semantics vary on modded lobbies; skip role-based checks there to avoid false positives.
            if (!Anticheat.IsModded())
            {
                if (RoleManager.IsImpostorRole(player.Data.RoleType))
                {
                    blockRpc = true;
                    Anticheat.Flag(player, $"{Anticheat.Name(player)} completed task {taskIndex} as an impostor.");
                    return;
                }

                if (taskIndex + 1 > (uint)player.Data.Tasks.Count)
                {
                    blockRpc = true;
                    Anticheat.Flag(player, $"{Anticheat.Name(player)} completed task {taskIndex} but only has {player.Data.Tasks.Count}.");
                }
            }
        }
    }

    internal class EnterVent : RpcCheck
    {
        public override Type GetExpectedNetObject() => typeof(PlayerPhysics);

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (ShipStatus.Instance == null)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} vented with no ShipStatus.");
                return;
            }

            if (Anticheat.IsModded()) return; // custom roles may vent

            if (!player.Data.IsDead && !player.Data.Role.CanVent)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} vented but role ({player.Data.RoleType}) can't vent.");
            }
        }
    }

    internal class ExitVent : RpcCheck
    {
        public override Type GetExpectedNetObject() => typeof(PlayerPhysics);

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (ShipStatus.Instance == null)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} exited a vent with no ShipStatus.");
                return;
            }

            if (Anticheat.IsModded()) return;

            if (!player.Data.IsDead && !player.Data.Role.CanVent)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} exited a vent but role ({player.Data.RoleType}) can't vent.");
            }
        }
    }

    internal class ClimbLadder : RpcCheck
    {
        public override Type GetExpectedNetObject() => typeof(PlayerPhysics);

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (ShipStatus.Instance == null)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} climbed a ladder with no ShipStatus.");
                return;
            }

            if (!player.Data.IsDead) return; // living players climbing is normal

            blockRpc = true;
            Anticheat.Flag(player, $"{Anticheat.Name(player)} climbed a ladder while dead.");
        }
    }

    // SnapTo (teleport) is only legitimate during gameplay, never in the lobby.
    internal class SnapTo : RpcCheck
    {
        public override Type GetExpectedNetObject() => typeof(CustomNetworkTransform);

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            NetHelpers.ReadVector2(reader); // position - read to advance, value unused

            if (LobbyBehaviour.Instance != null)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} used SnapTo (teleport) inside the lobby.");

                // Snap them back so the illegitimate move doesn't stick for other clients.
                if (AmongUsClient.Instance.AmHost && player?.NetTransform != null)
                    player.NetTransform.RpcSnapTo(player.transform.position);
            }
        }
    }
}
