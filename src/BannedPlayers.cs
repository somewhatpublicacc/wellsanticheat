using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace WellsAntiCheat
{
    // A persistent banned-players list the host edits as free text (notepad style) in the panel.
    // Each non-comment line is matched against a joining player's name OR friend code
    // (case-insensitive). Matches are auto-kicked while you host.
    internal static class BannedPlayers
    {
        public static bool Enabled = true;

        // The editable buffer shown in the notepad. Persisted to disk on Save().
        public static string RawText = "";

        private static readonly HashSet<string> _entries = new(StringComparer.OrdinalIgnoreCase);
        private static string Path_ => System.IO.Path.Combine(Paths.ConfigPath, "NitroAntiCheat_banned.txt");

        public static void Load()
        {
            if (!File.Exists(Path_))
            {
                RawText =
                    "# Nitro banned players list.\n" +
                    "# One entry per line - a player NAME or a FRIEND CODE (e.g. name#1234).\n" +
                    "# Matching is case-insensitive. Lines starting with # are ignored.\n" +
                    "# Matching players are auto-kicked while you are the host.\n\n";
                File.WriteAllText(Path_, RawText);
            }
            else
            {
                RawText = File.ReadAllText(Path_);
            }
            Reparse();
        }

        public static void Save()
        {
            try { File.WriteAllText(Path_, RawText); }
            catch (Exception e) { WellsPlugin.Log.LogWarning($"Couldn't save banned list: {e.Message}"); }
            Reparse();
            Notifier.Show($"Banned list saved ({_entries.Count} entries).");
        }

        private static void Reparse()
        {
            _entries.Clear();
            foreach (var raw in RawText.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                _entries.Add(line);
            }
        }

        // Add an entry from an in-game action and persist immediately.
        public static void Add(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return;
            if (!RawText.EndsWith("\n")) RawText += "\n";
            RawText += entry.Trim() + "\n";
            Save();
        }

        public static bool IsBanned(PlayerControl p)
        {
            if (!Enabled || p == null || p.Data == null || _entries.Count == 0) return false;
            var name = p.Data.PlayerName ?? "";
            var code = p.Data.FriendCode ?? "";
            return (name.Length > 0 && _entries.Contains(name))
                || (code.Length > 0 && _entries.Contains(code));
        }

        // For the GUI: current banned entries, and removal.
        public static List<string> EntryList()
        {
            var list = new List<string>(_entries);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public static void Remove(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return;
            var kept = new List<string>();
            foreach (var raw in RawText.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) { kept.Add(raw); continue; }
                if (string.Equals(line, entry.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                kept.Add(raw);
            }
            RawText = string.Join("\n", kept);
            Save();
        }
    }
}
