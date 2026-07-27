using Hazel;

namespace WellsAntiCheat.Rpc
{
    // Fake visual-task animation: crewmate-only, in-game, and only with visual tasks on.
    internal class PlayAnimation : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            reader.ReadByte(); // task type
            if (player?.Data == null) return;

            if (LobbyBehaviour.Instance != null)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} played a task animation inside the lobby.");
                blockRpc = true;
            }
            if (RoleManager.IsImpostorRole(player.Data.RoleType))
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} played a task animation as an impostor.");
                blockRpc = true;
            }
            if (GameManager.Instance != null && !GameManager.Instance.LogicOptions.GetVisualTasks())
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} played a visual task animation while visual tasks are off.");
                blockRpc = true;
            }
        }
    }

    // The Exiled RPC is never legitimately sent in-game.
    internal class Exiled : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            Anticheat.Flag(player, $"{Anticheat.Name(player)} sent an invalid Exiled RPC.");
            blockRpc = true;
        }
    }

    // SetColor payload validation (invalid netId / invalid colour). NOTE: deliberately NOT marked
    // host-only - SetColor is a host->client broadcast routed to the target, so a host-only check
    // here would kick legit players (the SetRole bug). These payload checks are sender-agnostic.
    internal class SetColor : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            uint netId = reader.ReadUInt32();
            byte color = reader.ReadByte();
            if (player?.Data == null) return;

            if (netId != player.Data.NetId)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"SetColor for {Anticheat.Name(player)} had a wrong net id (got {netId}).", false);
            }
            if (color >= Palette.ColorNames.Length)
            {
                blockRpc = true;
                Anticheat.Flag(player, $"SetColor for {Anticheat.Name(player)} had an invalid colour ({color}).", false);
            }
        }
    }

    // Fake medbay scan: needs the map spawned, a crewmate with the scan task, visual tasks on.
    internal class SetScanner : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            bool scanning = reader.ReadBoolean();
            if (player?.Data == null || !scanning) return;

            // When visual tasks are OFF, a scan is not a reliable signal (the scan task may not be
            // a visual task), so validating it produces false positives. Skip entirely.
            if (GameManager.Instance != null && !GameManager.Instance.LogicOptions.GetVisualTasks())
                return;

            if (ShipStatus.Instance == null)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} scanned before the map spawned.");
                blockRpc = true;
                return;
            }

            if (RoleManager.IsImpostorRole(player.Data.RoleType))
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} used a medbay scan as an impostor.");
                blockRpc = true;
                return;
            }

            // Only flag "no scan task" when we can actually see the player's task list; if it's
            // empty/unavailable we can't confirm, so we don't flag.
            bool hasTasks = player.Data.Tasks != null && player.Data.Tasks.Count > 0;
            if (!hasTasks) return;

            bool hasScanTask = false;
            foreach (NetworkedPlayerInfo.TaskInfo task in player.Data.Tasks)
            {
                if (task.TypeId == (byte)TaskTypes.SubmitScan) { hasScanTask = true; break; }
            }
            if (!hasScanTask)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} scanned without having the medbay scan task.");
                blockRpc = true;
            }
        }
    }

    // Airship platform: only valid on Airship, in-game, outside Hide and Seek.
    internal class UsePlatform : RpcCheck
    {
        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            if (player?.Data == null) return;

            if (MapUtil.GetCurrentMap() != MapNames.Airship)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} used a platform on the wrong map.");
                blockRpc = true;
                return;
            }
            if (ShipStatus.Instance == null)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} used a platform with no map spawned.");
                blockRpc = true;
                return;
            }
            if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek())
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} used a platform in Hide and Seek.");
                blockRpc = true;
            }
        }
    }

    // Level spoof: vanilla bans over 100k; we flag over 10k. Never blocked (cosmetic only).
    internal class SetLevel : RpcCheck
    {
        private const uint MaxLevel = 10000;

        public override void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc)
        {
            uint level = reader.ReadPackedUInt32();
            if (player?.Data == null) return;

            if (level > MaxLevel)
            {
                Anticheat.Flag(player, $"{Anticheat.Name(player)} sent an impossibly high level ({level}).");
                blockRpc = true;
            }
            if (ShipStatus.Instance != null)
                Anticheat.Flag(player, $"{Anticheat.Name(player)} sent SetLevel after the game started.", false);
        }
    }
}
