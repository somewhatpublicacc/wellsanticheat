using HarmonyLib;
using System.Collections.Generic;

namespace WellsAntiCheat
{
    // "Mute" in Among Us = block a player's chat RPCs host-side so their messages never propagate
    // (the game has no native mute). Mutes last for the current meeting and clear when it ends.
    //
    // Two optional triggers:
    //   A) Majority vote  - once a player holds a majority of votes (about to be ejected), mute
    //      them so a dying impostor can't leak in chat.
    //   B) Chat consensus - if a majority of living players type a colour name, the player wearing
    //      that colour is muted for the rest of the meeting.
    internal static class MuteManager
    {
        public static bool MuteOnMajorityVote = false;
        public static bool MuteOnChatConsensus = false;

        private static readonly HashSet<byte> _muted = new();
        // colourId -> set of distinct living voters who named that colour this meeting
        private static readonly Dictionary<int, HashSet<byte>> _colorVotes = new();
        private static Dictionary<string, int> _colorLookup;

        private static bool AmHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

        public static void ClearMeeting()
        {
            _muted.Clear();
            _colorVotes.Clear();
        }

        public static bool IsMuted(PlayerControl p)
            => p != null && p.Data != null && _muted.Contains(p.PlayerId);

        private static void Mute(byte playerId, string reason)
        {
            if (_muted.Add(playerId))
                Notifier.Show(reason);
        }

        private static int AliveCount()
        {
            int n = 0;
            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                if (p?.Data != null && !p.Data.IsDead && !p.Data.Disconnected) n++;
            return n;
        }

        // --- Feature A: majority votes ---
        // Called each frame during a meeting (host only).
        public static void CheckMajorityVotes()
        {
            if (!MuteOnMajorityVote || !AmHost || MeetingHud.Instance == null) return;

            try
            {
                var tally = new Dictionary<byte, int>();
                foreach (PlayerVoteArea state in MeetingHud.Instance.playerStates)
                {
                    if (state == null || !state.DidVote) continue;
                    byte v = state.VotedFor;
                    if (v == 253 || v == 254 || v == 255) continue; // skip / missed / no vote
                    tally.TryGetValue(v, out int c);
                    tally[v] = c + 1;
                }

                int needed = AliveCount() / 2 + 1;
                foreach (var kv in tally)
                {
                    if (kv.Value >= needed)
                    {
                        var target = FindPlayerById(kv.Key);
                        if (target != null && !Anticheat.IsExempt(target))
                            Mute(kv.Key, $"{Anticheat.Name(target)} has majority votes - muted for the meeting.");
                    }
                }
            }
            catch { }
        }

        // --- Feature B: chat colour consensus ---
        public static void RecordChatColorVote(PlayerControl sender, string message)
        {
            if (!MuteOnChatConsensus || !AmHost || sender?.Data == null) return;
            if (sender.Data.IsDead) return; // dead players don't count
            if (string.IsNullOrEmpty(message)) return;

            int colorId = MatchColor(message);
            if (colorId < 0) return;

            if (!_colorVotes.TryGetValue(colorId, out var voters))
            {
                voters = new HashSet<byte>();
                _colorVotes[colorId] = voters;
            }
            voters.Add(sender.PlayerId);

            int needed = AliveCount() / 2 + 1;
            if (voters.Count >= needed)
            {
                var target = FindPlayerByColor(colorId);
                if (target != null && !Anticheat.IsExempt(target))
                    Mute(target.PlayerId, $"{Anticheat.Name(target)} was muted by chat vote ({voters.Count}).");
            }
        }

        private static PlayerControl FindPlayerById(byte id)
        {
            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                if (p?.Data != null && p.PlayerId == id) return p;
            return null;
        }

        private static PlayerControl FindPlayerByColor(int colorId)
        {
            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                if (p?.Data != null && p.Data.DefaultOutfit != null && p.Data.DefaultOutfit.ColorId == colorId)
                    return p;
            return null;
        }

        // Returns a colour id if the message names exactly one known colour, else -1.
        private static int MatchColor(string message)
        {
            BuildColorLookup();
            var words = message.ToLowerInvariant().Split(new[] { ' ', ',', '.', '!', '?', ';', ':' },
                System.StringSplitOptions.RemoveEmptyEntries);

            int found = -1;
            foreach (var w in words)
            {
                if (_colorLookup.TryGetValue(w, out int id))
                {
                    if (found >= 0 && found != id) return -1; // ambiguous (named two colours)
                    found = id;
                }
            }
            return found;
        }

        private static void BuildColorLookup()
        {
            if (_colorLookup != null) return;
            // Among Us colour order is fixed (index = ColorId). We match English names directly
            // rather than going through the translation layer (players type English anyway).
            _colorLookup = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["red"] = 0, ["blue"] = 1, ["green"] = 2, ["pink"] = 3, ["orange"] = 4,
                ["yellow"] = 5, ["black"] = 6, ["white"] = 7, ["purple"] = 8, ["brown"] = 9,
                ["cyan"] = 10, ["lime"] = 11, ["maroon"] = 12, ["rose"] = 13, ["banana"] = 14,
                ["gray"] = 15, ["grey"] = 15, ["tan"] = 16, ["coral"] = 17,
                // common shorthands
                ["purp"] = 8, ["cyian"] = 10, ["darkgreen"] = 2, ["lightgreen"] = 11,
            };
        }

        // Clear mutes at the start of each meeting.
        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        private static class OnMeetingStart
        {
            private static void Postfix() => ClearMeeting();
        }
    }
}
