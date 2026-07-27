using System.Collections.Generic;

namespace WellsAntiCheat
{
    internal static class MapUtil
    {
        // Current map, resolved from ShipStatus spawn id when in-game or game options in lobby.
        // Ported/simplified from Hydra's Utilities.GetCurrentMap.
        public static MapNames GetCurrentMap()
        {
            try
            {
                if (ShipStatus.Instance == null)
                {
                    if (AmongUsClient.Instance != null &&
                        AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
                        return (MapNames)AmongUsClient.Instance.TutorialMapId;
                    return (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
                }
                return (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
            }
            catch { return MapNames.Skeld; }
        }

        // Union of sabotage system types across all maps. Used to reject bogus sabotage targets
        // without porting Hydra's full per-map tables.
        public static readonly HashSet<SystemTypes> ValidSabotages = new()
        {
            SystemTypes.Reactor, SystemTypes.Laboratory, SystemTypes.HeliSabotage,
            SystemTypes.LifeSupp, SystemTypes.Comms, SystemTypes.Electrical,
            SystemTypes.MushroomMixupSabotage,
        };
    }
}
