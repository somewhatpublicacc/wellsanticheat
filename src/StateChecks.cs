using System.Collections.Generic;

namespace WellsAntiCheat
{
    // Rejects RPCs that are impossible for the current game state. Modelled on BetterAmongUs's
    // CheckCancelRPC state gates. Catches a large class of cheats without per-RPC logic:
    //  - changing cosmetics during active gameplay (color/hat/skin/visor/pet/nameplate)
    //  - firing gameplay RPCs while still in the lobby (murder, vent, vote, meeting, roles, ...)
    //  - non-host clients sending host-authority RPCs
    internal static class StateChecks
    {
        public static bool Enabled = true;
        public static bool CheckHostOnly   = true;
        public static bool CheckCosmetics  = true;
        public static bool CheckLobbyRpcs  = true;

        // Cosmetic-set RPCs. Legit only in the lobby / customization, never mid-round.
        private static readonly HashSet<byte> Cosmetic = Ids(
            RpcCalls.SetColor, RpcCalls.SetHatStr, RpcCalls.SetSkinStr,
            RpcCalls.SetVisorStr, RpcCalls.SetPetStr, RpcCalls.SetNamePlateStr);

        // Gameplay RPCs that must never arrive while the lobby is up.
        private static readonly HashSet<byte> LobbyIllegal = Ids(
            RpcCalls.StartMeeting, RpcCalls.ReportDeadBody, RpcCalls.SendChatNote, RpcCalls.CloseMeeting,
            RpcCalls.Exiled, RpcCalls.CastVote, RpcCalls.ClearVote, RpcCalls.AddVote, RpcCalls.VotingComplete,
            RpcCalls.SetRole, RpcCalls.ClimbLadder, RpcCalls.UsePlatform, RpcCalls.UseZipline,
            RpcCalls.CompleteTask, RpcCalls.BootFromVent, RpcCalls.EnterVent, RpcCalls.ExitVent,
            RpcCalls.CloseDoorsOfType, RpcCalls.CheckMurder, RpcCalls.MurderPlayer,
            RpcCalls.CheckShapeshift, RpcCalls.Shapeshift, RpcCalls.RejectShapeshift,
            RpcCalls.CheckProtect, RpcCalls.ProtectPlayer, RpcCalls.CheckAppear, RpcCalls.StartAppear,
            RpcCalls.CheckVanish, RpcCalls.StartVanish, RpcCalls.TriggerSpores, RpcCalls.CheckSpore);

        // Host-authority RPCs that a non-host client should never originate.
        private static readonly HashSet<byte> HostOnly = Ids(
            RpcCalls.SetTasks, RpcCalls.ExtendLobbyTimer, RpcCalls.CloseMeeting,
            RpcCalls.SyncSettings, RpcCalls.SetInfected, RpcCalls.SetRole);

        public static void Check(PlayerControl player, byte callId, bool senderIsHost, ref bool blockRpc)
        {
            if (!Enabled || player == null || blockRpc) return;

            if (CheckHostOnly && !senderIsHost && HostOnly.Contains(callId))
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} sent host-only RPC {(RpcCalls)callId} as a non-host.");
                return;
            }

            // The remaining gates only apply to non-host senders: host-authored RPCs (role
            // assignment, exile, meeting close) legitimately fire around game transitions.
            if (senderIsHost) return;

            bool inLobby = LobbyBehaviour.Instance != null;
            bool inGameplay = ShipStatus.Instance != null && LobbyBehaviour.Instance == null;

            if (CheckCosmetics && inGameplay && Cosmetic.Contains(callId))
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} changed cosmetics mid-game ({(RpcCalls)callId}).");
                return;
            }

            if (CheckLobbyRpcs && inLobby && LobbyIllegal.Contains(callId))
            {
                blockRpc = true;
                Anticheat.Flag(player, $"{Anticheat.Name(player)} sent gameplay RPC {(RpcCalls)callId} while in the lobby.");
            }
        }

        private static HashSet<byte> Ids(params RpcCalls[] calls)
        {
            var set = new HashSet<byte>();
            foreach (var c in calls) set.Add((byte)c);
            return set;
        }
    }
}
