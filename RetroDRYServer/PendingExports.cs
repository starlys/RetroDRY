using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroDRY
{
    /// <summary>
    /// Short-term cache of pending export requests, allowing clients to set up an export via a JSON channel and then access it
    /// via a browser GET.
    /// </summary>
    public class PendingExports
    {
        class Item
        {
            public DateTime CreatedAtUtc = DateTime.UtcNow;
            public string Id = Guid.NewGuid().ToString();
            public IUser User;
            public DatonKey DatonKey;
            public int MaxRows;

            public Item(IUser user, DatonKey datonKey, int maxRows)
            {
                User = user;
                DatonKey = datonKey;
                MaxRows = maxRows;
            }
        }

        readonly List<Item> items = new List<Item>(); //lock this on access

        /// <summary>
        /// Store a request to export a daton, and return the request key
        /// </summary>
        /// <param name="user"></param>
        /// <param name="datonKey"></param>
        /// <param name="maxRows"></param>
        public string StoreRequest(IUser user, DatonKey datonKey, int maxRows)
        {
            lock (items)
            {
                //clean old
                var tooOld = DateTime.UtcNow.AddMinutes(-2);
                for (int i = items.Count - 1; i >= 0; i--) if (items[i].CreatedAtUtc < tooOld) items.RemoveAt(i);

                //store request
                var item = new Item(user, datonKey, maxRows);
                items.Add(item);
                return item.Id;
            }
        }

        /// <summary>
        /// Get a previously stored request by the request key; exception if not found
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public (IUser?, DatonKey, int) RetrieveRequest(string key)
        {
            lock (items)
            {
                var item = items.FirstOrDefault(i => i.Id == key);
                if (item == null) throw new Exception("Request expired");
                return (item.User, item.DatonKey, item.MaxRows);
            }
        }
    }
}
