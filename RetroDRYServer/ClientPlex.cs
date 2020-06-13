using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Container for all client connections
    /// </summary>
    public class ClientPlex
    {
        private class SessionInfo
        {
            public readonly string SessionKey = Guid.NewGuid().ToString();
            public IUser User;
            public DateTime LastAccessed = DateTime.UtcNow;

            /// <summary>
            /// The daton keys that this client is subscribed to. Having a key listed here ensures that the daton
            /// will remain cached and that changes to it will be pushed to the client.
            /// The dictionary value is the verion that was last sent to the client.
            /// </summary>
            public readonly ConcurrentDictionary<DatonKey, string> Subscriptions = new ConcurrentDictionary<DatonKey, string>();

            /// <summary>
            /// Datons that need to be pushed to this client. The objects here have already been changed to meet view permission restructions for this client.
            /// Lock the list on access.
            /// </summary>
            public readonly List<Daton> DatonsToPush = new List<Daton>();

            public bool FlagPermissionsObsolete;

            /// <summary>
            /// Manages awaiting during long polling, and is only set during the time the long poll is waiting; otherwise this is null.
            /// The bool generic argument is not used
            /// </summary>
            public TaskCompletionSource<bool> LongPollingCompleter;
        }

        /// <summary>
        /// Items being pushed to the client as a long polling result
        /// </summary>
        public class PushGroup
        {
            public Daton[] Datons;
            public bool IncludePermissions;
        }

        /// <summary>
        /// current clients indexed by session key
        /// </summary>
        private readonly ConcurrentDictionary<string, SessionInfo> Sessions = new ConcurrentDictionary<string, SessionInfo>();

        public int SessionCount => Sessions.Count;

        /// <summary>
        /// Not optimized; use for diagnostics only
        /// </summary>
        public int SubscriptionCount => Sessions.Values.Sum(s => s.Subscriptions.Count);

        /// <summary>
        /// Create a session and return a unique key
        /// </summary>
        public string CreateSession(IUser user)
        {
            var client = new SessionInfo { User = user };
            Sessions[client.SessionKey] = client;
            return client.SessionKey;
        }

        /// <summary>
        /// Delete a session; note that caller should also handle locks separately
        /// </summary>
        public void DeleteSession(string sessionKey)
        {
            Sessions.TryRemove(sessionKey, out _);
        }

        /// <summary>
        /// Get a user by session key; null if not found
        /// </summary>
        public IUser GetUser(string sessionKey)
        {
            if (sessionKey == null) return null;
            if (!Sessions.TryGetValue(sessionKey, out SessionInfo client)) return null;
            client.LastAccessed = DateTime.UtcNow;
            return client.User;
        }

        /// <summary>
        /// Change the subscription state of a daton for a client
        /// </summary>
        public void ManageSubscribe(string sessionKey, DatonKey datonKey, string version, bool subscribe)
        {
            if (datonKey.IsNew) throw new Exception("Cannot subscribe to an unsaved persiston");
            if (!Sessions.TryGetValue(sessionKey, out var ses)) return;
            if (subscribe) ses.Subscriptions[datonKey] = version;
            else ses.Subscriptions.TryRemove(datonKey, out _);
        }

        /// <summary>
        /// Clean sessions that have not been accessed in 2 minutes
        /// </summary>
        /// <param name="callback">if provided, this is called with each session key removed</param>
        public void Clean(Action<string> callback, int secondsOld = 120)
        {
            DateTime cutoff = DateTime.UtcNow.AddSeconds(0 - secondsOld);
            foreach (var client in Sessions.Values)
            {
                if (client.LastAccessed < cutoff)
                {
                    Sessions.TryRemove(client.SessionKey, out _);
                    callback?.Invoke(client.SessionKey);
                }
            }
        }

        /// <summary>
        /// Start a long poll for the session
        /// </summary>
        /// <returns>Task that should be awaited with a timeout, or null if bad sessionkey</returns>
        internal  Task<bool> BeginLongPoll(string sessionKey)
        {
            if (!Sessions.TryGetValue(sessionKey, out var ses)) return null;
            var completer = new TaskCompletionSource<bool>();
            ses.LongPollingCompleter = completer;
            return completer.Task;
        }

        /// <summary>
        /// If there is anything to push to the client, return it while also clearing the accumulators of those items.
        /// If there is nothing or unknown user, return null.
        /// </summary>
        public PushGroup GetAndClearItemsToPush(string sessionKey)
        {
            if (!Sessions.TryGetValue(sessionKey, out var ses)) return null;
            
            //datons: move to local vars and clear from session
            Daton[] datons = null;
            lock (ses.DatonsToPush)
            {
                if (ses.DatonsToPush.Any())
                {
                    datons = ses.DatonsToPush.ToArray();
                    ses.DatonsToPush.Clear();
                }
            }

            //other flags: move to local vars and clear from session
            bool includePermissions = ses.FlagPermissionsObsolete;
            ses.FlagPermissionsObsolete = false;

            //if anything found above, return the container else return null
            if (datons != null || includePermissions)
            {
                return new PushGroup
                {
                    Datons = datons,
                    IncludePermissions = includePermissions
                };
            }
            return null;
        }

        /// <summary>
        /// Determine if any subscription from any client is out of date; that is, if there is a client
        /// whose latest version is not the new version provided for the daton key provided.
        /// </summary>
        public bool IsAnyOutOfDate(DatonKey key, string newVersion)
        {
            foreach (var cli in Sessions.Values)
            {
                if (!cli.Subscriptions.TryGetValue(key, out string clientsVersion)) continue;
                if (clientsVersion != newVersion) return true;
            }
            return false;
        }

        /// <summary>
        /// Notify a subset of clients of changed permissions.
        /// </summary>
        /// <param name="user"></param>
        public void NotifyClientsOfPermissionChange(IUser user)
        {
            foreach (var cli in Sessions.Values)
            {
                if (cli.User.Id == user.Id)
                {
                    cli.FlagPermissionsObsolete = true;
                    var completer = cli.LongPollingCompleter; //another thread could change cli.LongPollingCompleter, so access only through local var
                    if (completer != null) completer.SetResult(true); 
                }
            }
        }

        /// <summary>
        /// Notify clients of changed persistons. This should be called when any persiston changes, in case a client is subscribed to it.
        /// No action will be taken if the change didn't change the version, so it is performant to call this repeatedly, or if it might
        /// not have really changed.
        /// </summary>
        public void NotifyClientsOf(DataDictionary dbdef, Daton[] datons)
        {
            foreach (var cli in Sessions.Values)
            {
                bool thisClientChanged = false;
                foreach (var daton in datons)
                {
                    //skip if this user doesn't need this daton
                    if (!cli.Subscriptions.TryGetValue(daton.Key, out string version)) continue;
                    if (version == daton.Version) continue; //don't send if client already has the latest version

                    //remember we sent it
                    cli.Subscriptions[daton.Key] = daton.Version;
                    thisClientChanged = true;

                    //hide cols by permissions
                    var datondef = dbdef.FindDef(daton);
                    var trimmedDaton = daton.Clone(datondef);
                    var guard = new SecurityGuard(dbdef, cli.User);
                    guard.HidePrivateParts(trimmedDaton);

                    //queue it for sending
                    lock (cli.DatonsToPush)
                    {
                        cli.DatonsToPush.Add(trimmedDaton);
                    }
                }

                //send queued datons now
                if (thisClientChanged)
                {
                    var completer = cli.LongPollingCompleter; //another thread could change cli.LongPollingCompleter, so access only through local var
                    if (completer != null) completer.SetResult(true); 
                }
            }
        }

        /// <summary>
        /// Get the combined set of subscribed daton keys for the subscriptions of all sessions; this is used to ensure those items stay cached
        /// </summary>
        /// <returns></returns>
        public HashSet<DatonKey> GetSubscriptions()
        {
            var ret = new HashSet<DatonKey>();
            foreach (var client in Sessions.Values)
            {
                foreach (var key in client.Subscriptions.Keys) ret.Add(key);
            }
            return ret;
        }
    }
}
