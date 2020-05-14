using System;
using System.Collections.Concurrent;

namespace RetroDRY
{
    public class DatonCache
    {
        private class Item
        {
            public DateTime LastAccessedUtc = DateTime.UtcNow;
            public Daton Daton;
        }

        private readonly ConcurrentDictionary<DatonKey, Item> Cache = new ConcurrentDictionary<DatonKey, Item>();

        public Daton Get(DatonKey key)
        {
            if (Cache.TryGetValue(key, out Item i))
            {
                i.LastAccessedUtc = DateTime.UtcNow;
                return i.Daton;
            }
            return null;
        }

        public void Put(Daton daton)
        {
            Cache[daton.Key] = new Item
            {
                Daton = daton
            };
        }

        /// <summary>
        /// Clean cache of anything that hasn't been accessed in 2 minutes, but keep anything that a client session has subscribed to
        /// </summary>
        public void Clean(ClientPlex clients)
        {
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-2);
            var sacredKeys = clients.GetSubscriptions();
            foreach (var key in Cache.Keys)
            {
                if (sacredKeys.Contains(key)) continue;
                if (!Cache.TryGetValue(key, out Item item)) continue;
                if (item.LastAccessedUtc < cutoff) Cache.TryRemove(key, out _);
            }
        }
    }
}
