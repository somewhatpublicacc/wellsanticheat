using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;

namespace WellsAntiCheat
{
    // Decides whether a requested name is offensive. The blocklist lives in a plain-text file
    // (BepInEx/config/WellsAntiCheat_blocklist.txt) so you can edit it without recompiling.
    internal static class NameFilter
    {
        public static bool Enabled = true;

        private static readonly HashSet<string> Blocked = new(StringComparer.Ordinal);
        private static string BlocklistPath => Path.Combine(Paths.ConfigPath, "WellsAntiCheat_blocklist.txt");

        // Common substitutions people use to smuggle slurs past a naive substring match.
        private static readonly Dictionary<char, char> LeetMap = new()
        {
            ['0'] = 'o', ['1'] = 'i', ['!'] = 'i', ['|'] = 'i',
            ['3'] = 'e', ['4'] = 'a', ['@'] = 'a', ['5'] = 's',
            ['$'] = 's', ['7'] = 't', ['+'] = 't', ['8'] = 'b',
            ['9'] = 'g', ['6'] = 'g', ['2'] = 'z',
        };

        // A minimal seed list so the mod is functional on first run. Add your own terms to the
        // config file. Kept short on purpose; the file is where the real list should live.
        private static readonly string[] SeedList =
        {
            // named-player kick target requested by the host
            "antipride",
            // slurs / hate terms (normalized form, i.e. what a name collapses to)
            "nigger", "nigga", "faggot", "retard", "kike", "spic", "chink", "tranny",
            // add more via the config file
        };

        public static void Load()
        {
            Blocked.Clear();

            if (!File.Exists(BlocklistPath))
            {
                var header =
                    "# WellsAntiCheat offensive-name blocklist.\n" +
                    "# One term per line. Lines starting with # are comments.\n" +
                    "# Matching is case-insensitive and leetspeak-aware:\n" +
                    "#   'nigger' also catches 'N1GG3R', 'n i g g e r', 'niiiggerr', etc.\n" +
                    "# Enter each term in plain lowercase letters.\n\n";
                File.WriteAllText(BlocklistPath, header + string.Join("\n", SeedList) + "\n");
            }

            foreach (var raw in File.ReadAllLines(BlocklistPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var norm = Normalize(line);
                if (norm.Length > 0) Blocked.Add(norm);
            }

            WellsPlugin.Log.LogInfo($"[WellsAntiCheat] Loaded {Blocked.Count} blocklist term(s).");
        }

        // Returns true and the offending term if the name should be blocked.
        public static bool IsOffensive(string name, out string matched)
        {
            matched = null;
            if (!Enabled || string.IsNullOrEmpty(name)) return false;

            var norm = Normalize(name);
            if (norm.Length == 0) return false;

            foreach (var term in Blocked)
            {
                if (norm.Contains(term))
                {
                    matched = term;
                    return true;
                }
            }
            return false;
        }

        // Lowercase, strip formatting tags, apply leet map, drop non-letters, collapse runs of
        // the same letter so "niiigger" -> "niger"... wait: collapsing before match would break
        // legit words, so we collapse to a single repeat max, which still catches padding tricks.
        public static string Normalize(string input)
        {
            // remove TMP rich-text tags like <color=...> that could hide characters
            var noTags = StripTags(input).ToLowerInvariant();

            var sb = new StringBuilder(noTags.Length);
            foreach (var ch in noTags)
            {
                char c = LeetMap.TryGetValue(ch, out var mapped) ? mapped : ch;
                if (c >= 'a' && c <= 'z') sb.Append(c);
                // everything else (spaces, punctuation, emoji) is dropped, which defeats
                // "n.i.g.g.e.r" and "n i g g e r" style spacing.
            }

            // Collapse 3+ identical letters down to 2 so "niiiiigger" normalizes toward "niigger",
            // then also produce a fully de-duplicated pass for matching.
            return Deduplicate(sb.ToString());
        }

        private static string StripTags(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool inTag = false;
            foreach (var ch in s)
            {
                if (ch == '<') { inTag = true; continue; }
                if (ch == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(ch);
            }
            return sb.ToString();
        }

        // Collapse any run of the same character to a single instance. This makes padding like
        // "niiiggggerr" match "niger"-style stored terms IF the stored term is also deduped.
        // We dedupe both sides, so store terms are deduped at load implicitly via this same call.
        private static string Deduplicate(string s)
        {
            if (s.Length == 0) return s;
            var sb = new StringBuilder(s.Length);
            char prev = '\0';
            foreach (var ch in s)
            {
                if (ch != prev) sb.Append(ch);
                prev = ch;
            }
            return sb.ToString();
        }
    }
}
