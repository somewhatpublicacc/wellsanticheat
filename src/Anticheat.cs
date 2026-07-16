using HarmonyLib;
using Hazel;
using WellsAntiCheat.Rpc;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WellsAntiCheat
{
    // RPC-validation dispatcher. Detection always runs and notifies; DISCARDING an RPC and
    // PUNISHING a player only happen when you are the host. Your own RPCs are never checked.
    internal static class Anticheat
    {
        public static bool Enabled = true;

        public static bool ModdedLobby = false;
        public static bool IsModded() => ModdedLobby || Constants.IsVersionModded();

        // crash / flood protection
        public static bool CrashProtection = true;
        public static bool CheckMalformed  = true;
        public static bool CheckFlood      = true;
        public static int  FloodThreshold  = 50;
        public static float FloodWindow    = 1.0f;

        // unknown/unregistered RPC detection (off in modded lobbies, which use custom RPCs)
        public static bool DetectUnknownRpc = true;

        public static readonly Dictionary<RpcCalls, RpcCheck> RpcHandlers = new()
        {
            { RpcCalls.CompleteTask,     new CompleteTask() },
            { RpcCalls.CheckName,        new CheckName() },
            { RpcCalls.SetName,          new SetName() },
            { RpcCalls.SendChat,         new SendChat() },
            { RpcCalls.ReportDeadBody,   new ReportDeadBody() },
            { RpcCalls.SetStartCounter,  new SetStartCounter() },
            { RpcCalls.EnterVent,        new EnterVent() },
            { RpcCalls.ExitVent,         new ExitVent() },
            { RpcCalls.SnapTo,           new SnapTo() },
            { RpcCalls.ClimbLadder,      new ClimbLadder() },
            // role-exploit checks (auto-relaxed on modded lobbies)
            { RpcCalls.CheckMurder,      new CheckMurder() },
            { RpcCalls.MurderPlayer,     new MurderPlayer() },
            { RpcCalls.Shapeshift,       new Shapeshift() },
            { RpcCalls.StartVanish,      new StartVanish() },
            { RpcCalls.ProtectPlayer,    new ProtectPlayer() },
        };

        private static readonly Dictionary<RpcCalls, int> MinBytes = new()
        {
            { RpcCalls.PlayAnimation, 1 }, { RpcCalls.CompleteTask, 1 }, { RpcCalls.SyncSettings, 1 },
            { RpcCalls.SetInfected, 1 }, { RpcCalls.CheckName, 1 }, { RpcCalls.SetName, 1 },
            { RpcCalls.CheckColor, 1 }, { RpcCalls.SetColor, 1 }, { RpcCalls.ReportDeadBody, 1 },
            { RpcCalls.MurderPlayer, 1 }, { RpcCalls.SendChat, 1 }, { RpcCalls.StartMeeting, 1 },
            { RpcCalls.SetScanner, 2 }, { RpcCalls.SendChatNote, 2 }, { RpcCalls.SetStartCounter, 1 },
            { RpcCalls.EnterVent, 1 }, { RpcCalls.ExitVent, 1 }, { RpcCalls.SnapTo, 8 },
            { RpcCalls.VotingComplete, 1 }, { RpcCalls.CastVote, 2 }, { RpcCalls.AddVote, 1 },
            { RpcCalls.CloseDoorsOfType, 1 }, { RpcCalls.SetTasks, 1 }, { RpcCalls.ClimbLadder, 2 },
        };

        // Set of valid vanilla RpcCalls ids, built once. If reflection fails in IL2CPP, the set
        // stays null and unknown-RPC detection safely no-ops.
        private static HashSet<byte> _knownRpcIds;
        private static bool _knownRpcTried;
        private static HashSet<byte> KnownRpcIds()
        {
            if (_knownRpcTried) return _knownRpcIds;
            _knownRpcTried = true;
            try
            {
                var set = new HashSet<byte>();
                foreach (RpcCalls r in Enum.GetValues(typeof(RpcCalls))) set.Add((byte)r);
                if (set.Count > 0) _knownRpcIds = set;
            }
            catch { _knownRpcIds = null; }
            return _knownRpcIds;
        }

        public enum Punishments { None, Kick, Ban }

        public static Punishments Punishment = Punishments.Kick;
        public static bool SendNotification = true;
        public static bool DiscardRpc = true;

        private static readonly RateTracker _rpcRate = new();

        private static bool AmHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
        private static bool IsSelf(PlayerControl player)
            => player != null && (player.AmOwner || player == PlayerControl.LocalPlayer);

        // --- Harmony hooks ---

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
        private static class OnPlayerControlRpc
        {
            private static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader)
                => HandleRpc(typeof(PlayerControl), __instance, callId, reader);
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
        private static class OnPlayerPhysicsRpc
        {
            private static bool Prefix(PlayerPhysics __instance, byte callId, MessageReader reader)
                => HandleRpc(typeof(PlayerPhysics), __instance.myPlayer, callId, reader);
        }

        [HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
        private static class OnNetTransformRpc
        {
            private static bool Prefix(CustomNetworkTransform __instance, byte callId, MessageReader reader)
                => HandleRpc(typeof(CustomNetworkTransform), __instance.myPlayer, callId, reader);
        }

        // Returns false to swallow the RPC, true to let it run.
        private static bool HandleRpc(Type sourceNetObj, PlayerControl player, byte callId, MessageReader reader)
        {
            if (!Enabled) return true;
            if (IsSelf(player)) return true; // HOST EXEMPTION: never inspect our own RPCs

            RpcCalls rpc = (RpcCalls)callId;
            bool blockRpc = false;

            // 1. Cheat-client fingerprint detection (kicks SickoMenu / AUM / KillNetwork users).
            if (player != null && CheatClients.Check(player, callId, reader))
                return AmHost ? false : true;

            if (player != null)
            {
                // 2. Crash protection: malformed payload + RPC flood.
                if (CrashProtection)
                {
                    if (CheckMalformed && MinBytes.TryGetValue(rpc, out int min) && reader.Length < min)
                    {
                        Flag(player, $"{Name(player)} sent a malformed {rpc} RPC (crash attempt).");
                        blockRpc = true;
                    }
                    if (CheckFlood && !blockRpc)
                    {
                        int count = _rpcRate.Record(player.OwnerId, Time.realtimeSinceStartup, FloodWindow);
                        if (count > FloodThreshold)
                        {
                            Flag(player, $"{Name(player)} is flooding RPCs ({count}/{FloodWindow:0.#}s) - crash attempt.");
                            blockRpc = true;
                        }
                    }
                }

                // 3. Unregistered/unknown RPC (skipped on modded lobbies).
                if (!blockRpc && DetectUnknownRpc && !IsModded())
                {
                    var known = KnownRpcIds();
                    if (known != null && !known.Contains(callId))
                    {
                        Flag(player, $"{Name(player)} sent an unregistered RPC ({callId}) - possible cheat/crash.");
                        blockRpc = true;
                    }
                }

                // 4. State-based rejection (cosmetics mid-game, player actions in lobby).
                if (!blockRpc)
                    StateChecks.Check(player, callId, ref blockRpc);
            }

            // 5. Per-RPC validators.
            if (!blockRpc && RpcHandlers.TryGetValue(rpc, out var check) && check != null && check.Enabled)
            {
                if (check.GetExpectedNetObject() != sourceNetObj)
                    return AmHost ? false : true;

                if (AmHost && check.IsHostOnly())
                {
                    Flag(player, $"{Name(player)} sent host-only RPC {rpc} as a non-host.");
                    blockRpc = true;
                }
                else
                {
                    int savedPos = reader.Position;
                    try { check.Validate(player, reader, ref blockRpc); }
                    catch (Exception e)
                    {
                        WellsPlugin.Log.LogWarning($"Wells check for {rpc} threw: {e.Message}");
                        blockRpc = false;
                    }
                    reader.Position = savedPos;
                }
            }

            // Blocking only happens when host. Non-host = notify only.
            if (AmHost && DiscardRpc && blockRpc) return false;
            return true;
        }

        public static void Flag(PlayerControl player, string reason, bool shouldPunish = true)
        {
            WellsPlugin.Log.LogMessage($"[Nitro] {reason}");
            if (SendNotification) Notifier.Show(reason);
            if (AmHost && shouldPunish && !IsSelf(player)) Punish(player);
        }

        private static void Punish(PlayerControl player)
        {
            if (player == null) return;
            switch (Punishment)
            {
                case Punishments.None: break;
                case Punishments.Kick: AmongUsClient.Instance.KickPlayer(player.OwnerId, false); break;
                case Punishments.Ban:  AmongUsClient.Instance.KickPlayer(player.OwnerId, true);  break;
            }
        }

        public static string Name(PlayerControl p) => p?.Data?.PlayerName ?? "<unknown>";
    }
}
