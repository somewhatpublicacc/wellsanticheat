using HarmonyLib;
using Hazel;
using WellsAntiCheat.Rpc;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WellsAntiCheat
{
    // RPC-validation dispatcher. Detection always runs and notifies; DISCARDING an RPC and
    // PUNISHING a player only happen when you are the host. Your own RPCs are never checked, so
    // the anti-cheat can never act against you.
    internal static class Anticheat
    {
        public static bool Enabled = true;

        // Manual "this is a modded lobby" switch. When on (or when the game build is modded),
        // role/gameplay-semantic checks relax to avoid false positives from role mods.
        public static bool ModdedLobby = false;
        public static bool IsModded() => ModdedLobby || Constants.IsVersionModded();

        // --- crash / flood protection thresholds (tunable) ---
        public static bool CrashProtection = true;
        public static int  FloodThreshold  = 50;   // RPCs within FloodWindow => flagged as flood
        public static float FloodWindow    = 1.0f;  // seconds

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
        };

        // Minimum payload byte counts. An RPC shorter than this is malformed and often a crash
        // attempt. Ported from HyperMenu's RpcValidator. Unknown RPCs (0) pass through.
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

        public enum Punishments { None, Kick, Ban }

        public static Punishments Punishment = Punishments.Kick;
        public static bool SendNotification = true;
        public static bool DiscardRpc = true;

        private static readonly RateTracker _rpcRate = new();

        private static bool AmHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

        // True if this RPC belongs to us (the local player / host). We NEVER check our own RPCs.
        private static bool IsSelf(PlayerControl player)
            => player != null && (player.AmOwner || player == PlayerControl.LocalPlayer);

        // --- Harmony hooks: one per net object that routes RPCs ---

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
        private static class OnPlayerControlRpc
        {
            private static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader)
                => HandleRpc(typeof(PlayerControl), __instance, (RpcCalls)callId, reader);
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
        private static class OnPlayerPhysicsRpc
        {
            private static bool Prefix(PlayerPhysics __instance, byte callId, MessageReader reader)
                => HandleRpc(typeof(PlayerPhysics), __instance.myPlayer, (RpcCalls)callId, reader);
        }

        [HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
        private static class OnNetTransformRpc
        {
            private static bool Prefix(CustomNetworkTransform __instance, byte callId, MessageReader reader)
                => HandleRpc(typeof(CustomNetworkTransform), __instance.myPlayer, (RpcCalls)callId, reader);
        }

        // Returns false to swallow the RPC (Harmony convention), true to let it run.
        private static bool HandleRpc(Type sourceNetObj, PlayerControl player, RpcCalls rpc, MessageReader reader)
        {
            if (!Enabled) return true;

            // HOST EXEMPTION: our own RPCs are never inspected, flagged, blocked, or punished.
            if (IsSelf(player)) return true;

            bool blockRpc = false;

            // --- crash protection: runs on ALL RPCs, before the per-RPC checks ---
            if (CrashProtection && player != null)
            {
                // Malformed (too-short) payload.
                if (MinBytes.TryGetValue(rpc, out int min) && reader.Length < min)
                {
                    Flag(player, $"{Name(player)} sent a malformed {rpc} RPC (crash attempt).");
                    blockRpc = true;
                }

                // RPC flood.
                int count = _rpcRate.Record(player.OwnerId, Time.realtimeSinceStartup, FloodWindow);
                if (count > FloodThreshold)
                {
                    Flag(player, $"{Name(player)} is flooding RPCs ({count}/{FloodWindow:0.#}s) - crash attempt.");
                    blockRpc = true;
                }
            }

            if (RpcHandlers.TryGetValue(rpc, out var check) && check != null && check.Enabled && !blockRpc)
            {
                // Wrong net object for this RPC => malformed/exploit. Drop it (host only).
                if (check.GetExpectedNetObject() != sourceNetObj)
                    return AmHost ? false : true;

                // Only the host should originate host-only RPCs.
                if (AmHost && check.IsHostOnly())
                {
                    Flag(player, $"{Name(player)} sent host-only RPC {rpc} as a non-host.");
                    blockRpc = true;
                }
                else
                {
                    int savedPos = reader.Position;
                    try
                    {
                        check.Validate(player, reader, ref blockRpc);
                    }
                    catch (Exception e)
                    {
                        WellsPlugin.Log.LogWarning($"Wells check for {rpc} threw: {e.Message}");
                        blockRpc = false;
                    }
                    reader.Position = savedPos; // rewind for vanilla HandleRpc
                }
            }

            // Blocking/discarding only happens when we are host. Non-host = notify only.
            if (AmHost && DiscardRpc && blockRpc) return false;
            return true;
        }

        public static void Flag(PlayerControl player, string reason, bool shouldPunish = true)
        {
            WellsPlugin.Log.LogMessage($"[Wells] {reason}");
            if (SendNotification)
                Notifier.Show(reason);

            // Punishment is host-only, and never applies to ourselves (self is already exempt).
            if (AmHost && shouldPunish && !IsSelf(player))
                Punish(player);
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
