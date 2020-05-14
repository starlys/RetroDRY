using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Top level class for implementing RetroDRY. Generally there is only one of these instances, which the developer should
    /// create and store globally.
    /// </summary>
    public class Retroverse
    {
        /// <summary>
        /// Get the database definition (metadata, schema)
        /// </summary>
        public DataDictionary DataDictionary { get; private set; }

        /// <summary>
        /// Page size applied to loading viewons' main table
        /// </summary>
        public int ViewonPageSize = 500;

        /// <summary>
        /// All connected clients
        /// </summary>
        private readonly ClientPlex ClientPlex = new ClientPlex();
        
        /// <summary>
        /// Cache of datons for all clients
        /// </summary>
        private readonly DatonCache DatonCache = new DatonCache();

        /// <summary>
        /// data access implmentation for all types; also see SqlOverrides
        /// </summary>
        private RetroSql DefaultSql;

        /// <summary>
        /// data access implmentation override by type; indexed by daton type name
        /// </summary>
        private readonly ConcurrentDictionary<string, RetroSql> SqlOverrides = new ConcurrentDictionary<string, RetroSql>();

        /// <summary>
        /// Injected function to get a database connection by database number (for most applications, the number is always 0)
        /// </summary>
        public Func<int, DbConnection> GetDbConnection { get; private set; }

        internal readonly LockManager LockManager;

        /// <summary>
        /// Host app can use this to run background tasks
        /// </summary>
        public BackgroundWorker BackgroundWorker = new BackgroundWorker();

        internal readonly SqlFlavorizer.VendorKind DatabaseVendor;

        /// <summary>
        /// Construct
        /// </summary>
        /// <param name="connectionResolver">Host app function to get an open database connection, which will be disposed after each use</param>
        public Retroverse(SqlFlavorizer.VendorKind dbVendor, DataDictionary ddict, Func<int, DbConnection> connectionResolver/*, Func<string, string, IUser> userResolver*/,
            int lockDatabaseNumber = 0)
        {
            if (!ddict.IsFinalized) throw new Exception("DataDictionary must be finalized first");
            DatabaseVendor = dbVendor;
            DataDictionary = ddict;
            GetDbConnection = connectionResolver;
            LockManager = new LockManager(() => GetDbConnection(lockDatabaseNumber), PropogatePersistonChanged);
            DefaultSql = new RetroSql();
            DefaultSql.Initialize(DatabaseVendor);

            //set up process to clean cache
            BackgroundWorker.Register(() =>
            {
                DatonCache.Clean(ClientPlex);
                return Task.CompletedTask;
            }, 60);

            //set up process to clean abandoned sessions
            void clientCleanerCallback(string sessionKey) => LockManager.ReleaseLocksForSession(sessionKey);
            BackgroundWorker.Register(() =>
            {
                ClientPlex.Clean(clientCleanerCallback);
                return Task.CompletedTask;
            }, 70);

            //set up process for communcation with other servers about locks
            BackgroundWorker.Register(DoLockRefresh, 90);
        }

        /// <summary>
        /// Inject the implementation of RetroSql for all types. 
        /// </summary>
        public void OverrideAllSql(RetroSql r)
        {
            r.Initialize(DatabaseVendor);
            DefaultSql = r;
        }

        /// <summary>
        /// Inject the implementation of RetroSql for the given type name
        /// </summary>
        /// <param name="typeName">matches Daton subclass name</param>
        public void OverrideSql(string typeName, RetroSql r)
        {
            r.Initialize(DatabaseVendor);
            SqlOverrides[typeName] = r;
        }

        /// <summary>
        /// Notify RetroDRY that the host app has changed permissions for a user; this causes the permission set to be sent to connected sessions for the user
        /// </summary>
        /// <param name="user"></param>
        public void NotifyPermissionsChanged(IUser user)
        {
            ClientPlex.NotifyClientsOfPermissionChange(user);
        }

        /// <summary>
        /// Host app must call this when receiving http request on POST /api/retro/main
        /// </summary>
        public async Task<MainResponse> HandleHttpMain(MainRequest req)
        {
            var user = ClientPlex.GetUser(req.SessionKey); 
            if (user == null) return new MainResponse { ErrorCode = Constants.ERRCODE_BADUSER };
            var resp = new MainResponse();

            //initialize
            if (req.Initialze != null)
            {
                resp.PermissionSet = Retrovert.PermissionsToWire(user);
                resp.DataDictionary = Retrovert.DataDictionaryToWire(DataDictionary, user);
            }

            //load datons
            if (req.GetDatons != null)
            {
                var datons = new List<Daton>();
                foreach (var drequest in req.GetDatons)
                {
                    var daton = await GetDaton(DatonKey.Parse(drequest.Key), user);
                    if (drequest.KnownVersion != daton.Version) //omit if client already has the current version
                        datons.Add(daton); 
                    if (drequest.DoSubscribe) 
                        ClientPlex.ManageSubscribe(req.SessionKey, daton.Key, daton.Version, true);
                }
                resp.CondensedDatons = datons.Select(daton =>
                {
                    bool isComplete = (daton is Viewon v) ? v.IsCompleteLoad : true;
                    return new CondensedDatonResponse
                    {
                        IsComplete = isComplete,
                        CondensedDatonJson = Retrovert.ToWire(DataDictionary, daton, false)
                    };
                }).ToArray();
            }

            //save datons
            if (req.SaveDatons != null)
            {
                var diffs = new List<PersistonDiff>();
                foreach (var saveRequest in req.SaveDatons)
                {
                    var diff = Retrovert.FromDiff(DataDictionary, saveRequest.Diff);
                    diffs.Add(diff);
                }
                var results = await SaveDatons(req.SessionKey, user, diffs.ToArray());

                var saveResponses = new List<SavePersistonResponse>();
                foreach (var result in results)
                {
                    saveResponses.Add(new SavePersistonResponse
                    {
                        IsDeleted = result.IsDeleted,
                        IsSuccess = result.IsSuccess,
                        OldKey = result.OldKey.ToString(),
                        NewKey = result.NewKey.ToString(),
                        Errors = result.Errors
                    });
                }
                resp.SavedPersistons = saveResponses.ToArray();
            }

            //change datons state
            if (req.ManageDatons != null)
            {
                var manageResponses = new List<ManageDatonResponse>(req.ManageDatons.Length);
                foreach (var mrequest in req.ManageDatons)
                {
                    //what does the caller wants to change?
                    var datonKey = DatonKey.Parse(mrequest.Key);
                    bool wantsLock = mrequest.SubscribeState == 2;
                    bool wantsSubscribe = mrequest.SubscribeState >= 1;

                    //handle change in subscription
                    //(Performance note: unsubscribe should happen before unlock so that the unlock-propogation can short circuit reloading. Ultimately
                    //if only one client is dealing with a daton and that client releases the lock and subscription, this server can forget about it
                    //immediately.)
                    ClientPlex.ManageSubscribe(req.SessionKey, datonKey, mrequest.Version, wantsSubscribe);
                    bool isSubscribed = wantsSubscribe;

                    //handle change in lock
                    string lockErrorCode = "";
                    bool hasLock = false;
                    if (wantsLock)
                    {
                        (hasLock, lockErrorCode) = LockManager.RequestLock(datonKey, mrequest.Version, req.SessionKey);
                    }
                    else 
                    {
                        LockManager.ReleaseLock(datonKey, req.SessionKey);
                    }

                    manageResponses.Add(new ManageDatonResponse
                    {
                        ErrorCode = lockErrorCode,
                        Key = mrequest.Key,
                        SubscribeState = hasLock ? 2 : (isSubscribed ? 1 : 0)
                    });
                }
                resp.ManageDatons = manageResponses.ToArray();
            }

            //quit - free up locks and memory
            if (req.DoQuit)
            {
                ClientPlex.DeleteSession(req.SessionKey);
                LockManager.ReleaseLocksForSession(req.SessionKey);
            }

            return resp;
        }

        /// <summary>
        /// Host app must call this when receiving http request on POST /api/retro/long
        /// </summary>
        public async Task<LongResponse> HandleHttpLong(LongRequest req)
        {
            var user = ClientPlex.GetUser(req.SessionKey);
            if (user == null) return new LongResponse { ErrorCode = Constants.ERRCODE_BADUSER };

            //if there is already something to push, then return now, without any awaiting
            var pushGroup = ClientPlex.GetAndClearItemsToPush(req.SessionKey);
            if (pushGroup != null) return PushGroupToLongResponse(user, pushGroup);

            //wait up to 30s for something to be ready to send to the client
            var pollTask = ClientPlex.BeginLongPoll(req.SessionKey);
            if (pollTask == null) return new LongResponse { ErrorCode = Constants.ERRCODE_BADUSER };
            var timeoutTask = Task.Delay(30000);
            bool timedOut = (await Task.WhenAny(pollTask, timeoutTask) == timeoutTask);
            
            //timeout - empty return
            if (timedOut) return new LongResponse();

            //send anything that has accumulated
            pushGroup = ClientPlex.GetAndClearItemsToPush(req.SessionKey);
            if (pushGroup != null) return PushGroupToLongResponse(user, pushGroup);

            //nothing to do
            return new LongResponse();
        }

        /// <summary>
        /// Get a daton, from cache or load from database. This is a shared instance so the caller may not modify it.
        /// </summary>
        /// <param name="user">if null, the return value is a shared guaranteed complete daton; if user is provided,
        /// the return value may be a clone with some rows removed or columns set to null</param>
        /// <param name="forceCheckLatest">if true then checks database to ensure latest version even if it was cached</param>
        public async Task<Daton> GetDaton(DatonKey key, IUser user, bool forceCheckLatest = false)
        {
            if (key.IsNew) throw new Exception("Cannot get daton that hasn't been persisted");

            //get from cache if possible, and optionally ignore cached version if it is not the latest
            string verifiedVersion = null;
            Daton daton = DatonCache.Get(key);
            if (forceCheckLatest && daton != null)
            {
                verifiedVersion = LockManager.GetVersion(key);
                if (verifiedVersion != daton.Version) daton = null;
            }

            //get from database if needed (and cache it)
            var datondef = DataDictionary.DatonDefs[key.Name];
            if (daton == null)
            {
                var sql = GetSqlInstance(key);
                using (var db = GetDbConnection(datondef.DatabaseNumber))
                    daton = await sql.Load(db, DataDictionary, key, ViewonPageSize);
                if (verifiedVersion == null)
                    verifiedVersion = LockManager.GetVersion(key);
                daton.Version = verifiedVersion;
                DatonCache.Put(daton);
            }

            //enforce permissions on the user
            if (user != null)
            {
                daton = daton.Clone(datondef);
                var guard = new SecurityGuard(DataDictionary, user);
                guard.HidePrivateParts(daton);
            }

            return daton;
        }

        /// <summary>
        /// Save one or more persiston changes. This will make any cached versions of the persiston obsolete, but
        /// will not do anything about the cache, and will not remember the newly assigned version.
        /// Also note that propogation of changes does not happen until the lock is released (not here).
        /// </summary>
        public async Task<MultiSaver.Result[]> SaveDatons(string sessionKey, IUser user, PersistonDiff[] diffs)
        {
            //confirm this user has locks
            var keysAndVersions = diffs.Select(d => (d.Key, d.BasedOnVersion));
            var lockError = ConfirmAllLocks(sessionKey, keysAndVersions);
            if (lockError != null)
                return new MultiSaver.Result[] { lockError };

            //save
            using (var saver = new MultiSaver(this, user, diffs))
            {
                await saver.Save();
                return saver.GetResults();
            }
        }

        /// <summary>
        /// Check whether the user holds locks on the given datons. If yes, returns null, else returns
        /// an error structure that can be returned to a caller, noting ONLY the first encountered problem.
        /// </summary>
        public MultiSaver.Result ConfirmAllLocks(string sessionKey, IEnumerable<(DatonKey, string)> datonKeysAndVersions)
        {
            //Note: The client would already know what locks they have so this would be an 
            //unusual error. 
            foreach ((var datonKey, string reqVersion) in datonKeysAndVersions)
            {
                if (datonKey.IsNew) continue;
                (_, bool isLockedByMe, string actualVersion) = LockManager.GetLockState(datonKey, sessionKey);
                if (!isLockedByMe)
                    return new MultiSaver.Result { OldKey = datonKey, Errors = new[] { Constants.ERRCODE_LOCK } };
                if (reqVersion != actualVersion)
                    return new MultiSaver.Result { OldKey = datonKey, Errors = new[] { Constants.ERRCODE_VERSION } };
            }
            return null;
        }

        /// <summary>
        /// Get the RetroSql instsance to use to load/save a daton
        /// </summary>
        public RetroSql GetSqlInstance(DatonKey key)
        {
            if (SqlOverrides.TryGetValue(key.Name, out RetroSql r)) return r;
            return DefaultSql;
        }

        /// <summary>
        /// Create a new session. The session will last until the client quits cleanly or stops sending long polling requests.
        /// </summary>
        /// <param name="user">This includes the security roles that RetroDRY will enforce</param>
        /// <returns>a session key which must be registered client side</returns>
        public string CreateSession(IUser user)
        {
            return ClientPlex.CreateSession(user);
        }

        /// <summary>
        /// This is ONLY called via LockManager after unlocked, and only when the persiston changed during the lock.
        /// So it only handles changes made by this server.
        /// </summary>
        private async Task PropogatePersistonChanged(DatonKey key, string version) 
        {
            if (ClientPlex.IsAnyOutOfDate(key, version))
            {
                var daton = await GetDaton(key, null, forceCheckLatest: true);
                ClientPlex.NotifyClientsOf(DataDictionary, new[] { daton }); //this needs to take security into account
            }
        }

        /// <summary>
        /// Restructure a ClientPlex.PushGroup into a LongResponse for sending via http
        /// </summary>
        private LongResponse PushGroupToLongResponse(IUser user, ClientPlex.PushGroup pg)
        {
            var wireDatons = new List<CondensedDatonResponse>();
            if (pg?.Datons != null)
            {
                foreach (var daton in pg.Datons)
                    wireDatons.Add(new CondensedDatonResponse
                    {
                        IsComplete = (daton is Viewon v ? v.IsCompleteLoad : false),
                        CondensedDatonJson = Retrovert.ToWire(DataDictionary, daton, false)
                    });
            }

            PermissionResponse permissions = null;
            if (pg.IncludePermissions)
                permissions = Retrovert.PermissionsToWire(user);

            return new LongResponse
            {
                CondensedDatons = wireDatons.ToArray(),
                PermissionSet = permissions

            };
        }

        /// <summary>
        /// Called periodically to touch locked records (communicating to other servers) and fetch changes
        /// (receiving from other servers) to multiplex to all subscribed clients
        /// </summary>
        private async Task DoLockRefresh()
        {
            List<(DatonKey, string)> otherServerUpdates = LockManager.InterServerProcess();
            var updatesWithSubscriptions = otherServerUpdates.Where(pair => ClientPlex.IsAnyOutOfDate(pair.Item1, pair.Item2)).ToArray();
            var datonsToPush = new List<Daton>(updatesWithSubscriptions.Length);
            foreach (var pair in updatesWithSubscriptions)
            {
                var daton = await GetDaton(pair.Item1, null, forceCheckLatest: true);
                datonsToPush.Add(daton);
            }
            ClientPlex.NotifyClientsOf(DataDictionary, datonsToPush.ToArray());
        }
    }
}
