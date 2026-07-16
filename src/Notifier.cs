using System;
using System.Collections.Generic;

namespace WellsAntiCheat
{
    // Minimal notification store. The GUI reads recent entries and draws them; entries expire.
    internal static class Notifier
    {
        private struct Entry { public string Text; public DateTime At; }

        private static readonly List<Entry> _entries = new();
        public static float DisplaySeconds = 10f;

        public static void Show(string text)
        {
            lock (_entries)
                _entries.Add(new Entry { Text = text, At = DateTime.UtcNow });
        }

        public static IEnumerable<string> Recent()
        {
            lock (_entries)
            {
                _entries.RemoveAll(e => (DateTime.UtcNow - e.At).TotalSeconds > DisplaySeconds);
                var copy = new List<string>(_entries.Count);
                foreach (var e in _entries) copy.Add(e.Text);
                return copy;
            }
        }
    }
}
