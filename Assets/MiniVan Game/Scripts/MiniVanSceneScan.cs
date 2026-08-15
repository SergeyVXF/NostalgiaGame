using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Short-TTL cache for FindObjectsByType scene scans. A full scan of a
    /// generated world costs ~0.5-1 ms, and interaction code used to run a
    /// dozen of them per frame. Prompts tolerate results up to half a second
    /// stale; destroyed entries surface as null and callers already skip null.
    /// </summary>
    public static class MiniVanSceneScan
    {
        private const float DefaultTtl = 0.4f;

        private static class Cache<T> where T : Object
        {
            public static T[] Items;
            public static float NextRefreshTime;
        }

        public static T[] Get<T>() where T : Object
        {
            if (Cache<T>.Items == null || Time.unscaledTime >= Cache<T>.NextRefreshTime)
            {
                Cache<T>.NextRefreshTime = Time.unscaledTime + DefaultTtl;
                Cache<T>.Items = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            }

            return Cache<T>.Items;
        }

        /// <summary>Force the next Get to rescan (e.g. right after spawning).</summary>
        public static void Invalidate<T>() where T : Object
        {
            Cache<T>.NextRefreshTime = 0f;
        }
    }
}
