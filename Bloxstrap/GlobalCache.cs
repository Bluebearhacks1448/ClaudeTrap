using System.Collections.Concurrent;

namespace Bloxstrap
{
    public static class GlobalCache
    {
        // Server location lookups can be triggered by the log reader and UI at the
        // same time, and may target different ActivityData snapshots.
        // Empty string represents a cached lookup failure because ConcurrentDictionary
        // intentionally does not permit null values.
        public static readonly ConcurrentDictionary<string, string> ServerLocation = new();
    }
}
