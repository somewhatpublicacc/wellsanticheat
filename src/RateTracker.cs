using System.Collections.Generic;

namespace WellsAntiCheat
{
    // Tracks timestamped events per player (keyed by OwnerId) and reports how many happened
    // within a sliding window. Used for chat-spam and RPC-flood (crash attempt) detection.
    internal class RateTracker
    {
        private readonly Dictionary<int, Queue<float>> _events = new();

        // Record an event for a player at `now` (Time.realtimeSinceStartup) and return the count
        // of events (including this one) within `windowSeconds`.
        public int Record(int ownerId, float now, float windowSeconds)
        {
            if (!_events.TryGetValue(ownerId, out var q))
            {
                q = new Queue<float>();
                _events[ownerId] = q;
            }

            q.Enqueue(now);
            while (q.Count > 0 && now - q.Peek() > windowSeconds)
                q.Dequeue();

            return q.Count;
        }

        public void Reset(int ownerId) => _events.Remove(ownerId);
        public void ResetAll() => _events.Clear();
    }
}
