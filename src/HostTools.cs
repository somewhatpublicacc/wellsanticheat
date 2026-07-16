using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace WellsAntiCheat
{
    // Host-only utilities. Everything here checks AmHost before doing anything, so the GUI can
    // gray these out but even a stray call is safe.
    internal static class HostTools
    {
        // Map ids: 0 Skeld, 1 MiraHQ, 2 Polus, 3 Dleks, 4 Airship, 5 Fungle.
        public static byte SelectedMap = 0;
        public const byte MaxMapId = 5;

        private static bool AmHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

        public static void DespawnMap()
        {
            if (!AmHost) return;
            if (ShipStatus.Instance != null)
            {
                ShipStatus.Instance.Despawn();
                Notifier.Show("Current map despawned.");
            }
            else Notifier.Show("No map is currently spawned.");
        }

        public static void SpawnMap()
        {
            if (!AmHost) return;
            AmongUsClient.Instance.StartCoroutine(SpawnMapRoutine(SelectedMap).WrapToIl2Cpp());
        }

        private static IEnumerator SpawnMapRoutine(byte mapId)
        {
            WellsPlugin.Log.LogInfo($"Spawning map id {mapId}");
            AsyncOperationHandle<GameObject> handle =
                AmongUsClient.Instance.ShipPrefabs[mapId].InstantiateAsync(null, false);
            yield return handle;

            ShipStatus ship = handle.Result.GetComponent<ShipStatus>();
            AmongUsClient.Instance.Spawn(ship, -2, SpawnFlags.None);
            Notifier.Show($"Map {(MapNames)mapId} spawned.");
        }

        public static void DespawnLobby()
        {
            if (!AmHost) return;
            if (LobbyBehaviour.Instance != null)
            {
                LobbyBehaviour.Instance.Despawn();
                Notifier.Show("Lobby despawned.");
            }
            else Notifier.Show("Lobby is already despawned.");
        }

        public static void SpawnLobby()
        {
            if (!AmHost) return;
            LobbyBehaviour.Instance = Object.Instantiate(GameStartManager.Instance.LobbyPrefab);
            AmongUsClient.Instance.Spawn(LobbyBehaviour.Instance, -2, SpawnFlags.None);
            Notifier.Show("Lobby spawned.");
        }

        public static void ForceCrewVictory()
        {
            if (!AmHost || GameManager.Instance == null) return;
            GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByTask, false);
            Notifier.Show("Forced crewmate victory.");
        }

        public static void ForceImpostorVictory()
        {
            if (!AmHost || GameManager.Instance == null) return;
            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill, false);
            Notifier.Show("Forced impostor victory.");
        }
    }
}
