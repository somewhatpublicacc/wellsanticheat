using System;
using System.Collections.Generic;

namespace WellsAntiCheat
{
    // Hidden, hardcoded exemption list. Anyone matched here is NEVER flagged, blocked, or punished
    // by Nitro - any role (crew or impostor), host or not. There is intentionally no GUI, config,
    // or file for this: it is baked into the build and not visible to users of the mod.
    //
    // Matching is by FRIEND CODE only (names change too often to be reliable).
    internal static class TrustedPlayers
    {
        private static readonly HashSet<string> TrustedFriendCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "homelessee#4582",
        };

        public static bool IsTrusted(PlayerControl p)
        {
            if (p == null || p.Data == null) return false;
            var code = p.Data.FriendCode ?? "";
            return code.Length > 0 && TrustedFriendCodes.Contains(code);
        }
    }
}
