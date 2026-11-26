using System.Collections.Concurrent;

namespace Bloxstrap
{
    public static class GlobalCache
    {
        // Server location lookups can be triggered by the log reader and UI at the
        // same time, and may target different ActivityData snapshots.
        public static readonly ConcurrentDictionary<string, string?> ServerLocation = new();
    }
}
