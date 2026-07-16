using System.Collections.Generic;

namespace WellsAntiCheat
{
    // Rejects RPCs that are impossible for the current game state.
    //
    // IMPORTANT: this runs from the per-object HandleRpc hook, where `player` is the net object
    // the RPC is ABOUT, not who SENT it. So we can only safely check RPCs that route to the
    // actor's own PlayerControl (a player's own action). Host->client broadcast RPCs (SetRole,
    // SetTasks, SetInfected, Exiled, VotingComplete, CloseMeeting, ...) route to a target that
    // isn't the sender, so we must NOT treat them here - doing so previously kicked whole lobbies
    // at role assignment. Proper host-only detection needs a sender-aware hook (see note below).
    internal static class StateChecks
    {
        public static bool Enabled = true;
        public static bool CheckCosmetics  = true;
        public static bool CheckLobbyRpcs  = true;

        // Cosmetic-set RPCs, which route to the changing player's own object. Legit only in the
        // lobby / customization, never mid-round.
        private static readonly HashSet<byte> Cosmetic = Ids(
            RpcCalls.SetColor, RpcCalls.SetHatStr, RpcCalls.SetSkinStr,
            RpcCalls.SetVisorStr, RpcCalls.SetPetStr, RpcCalls.SetNamePlateStr);

        // Player-self-authored action RPCs that route to the actor's own PlayerControl. These
        // require gameplay (ShipStatus) to be legitimate, so seeing one while the lobby is up is
        // anomalous. Host-authored/broadcast RPCs are deliberately excluded.
        private static readonly HashSet<byte> LobbyIllegal = Ids(
            RpcCalls.MurderPlayer, RpcCalls.CheckMurder,
            RpcCalls.EnterVent, RpcCalls.ExitVent, RpcCalls.BootFromVent,
            RpcCalls.ClimbLadder, RpcCalls.UsePlatform, RpcCalls.UseZipline, RpcCalls.CheckZipline,
            RpcCalls.CompleteTask,
            RpcCalls.Shapeshift, RpcCalls.CheckShapeshift, RpcCalls.RejectShapeshift,
            RpcCalls.ProtectPlayer, RpcCalls.CheckProtect,
            RpcCalls.StartVanish, RpcCalls.CheckVanish, RpcCalls.StartAppear, RpcCalls.CheckAppear,
            RpcCalls.TriggerSpores, RpcCalls.CheckSpore);

        public static void Check(PlayerControl player, byte callId, ref bool blockRpc)
        {
            if (!Enabled || player == null || blockRpc) return;

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
