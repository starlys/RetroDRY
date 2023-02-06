using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace RetroDRY
{
    /// <summary>
    /// Top level class for implementing RetroDRY. Generally there is only one of these instances, which the developer should
    /// create and store globally.
    /// </summary>
    public class Retroverse : IDisposable
    {
        /// <summary>
        /// Get the database definition (metadata, schema)
        /// </summary>
        public DataDictionary DataDictionary { get; private set; } = new DataDictionary();

        /// <summary>
        /// Injectable language messages by language code, whose message codes match those declared in Constants.EnglishMessages.
        /// If this is nonnull, then any messages here will override the default English messages based on the user's language.
        /// The usage is: LanguageMessages[langCode][messageCode] = message, where langCode may be empty string for the default language.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>>? LanguageMessages;

        /// <summary>
        /// Page size applied to loading viewons' main table
        /// </summary>
        public int ViewonPageSize = 500;

        /// <summary>
        /// Max rows for exports
        /// </summary>
        public int MaxExportRows = 500000;

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
        private RetroSql? DefaultSql;

        /// <summary>
        /// data access implmentation override by type; indexed by daton type name
        /// </summary>
        private readonly ConcurrentDictionary<string, RetroSql> SqlOverrides = new ConcurrentDictionary<string, RetroSql>();

        /// <summary>
        /// Injected function to get a database connection by environment and database number (for most applications, the number is always 0)
        /// </summary>
        public Func<int, Task<DbConnection>>? GetDbConnection { get; private set; }

        /// <summary>
        /// Injected function to allow host app to fix database exception messages, making them appropriate for use
        /// </summary>
        public Func<IUser, Exception, string>? CleanUpSaveException;

        internal LockManager? LockManager;

        /// <summary>
        /// Host app can use this to run background tasks
        /// </summary>
        public BackgroundWorker BackgroundWorker = new BackgroundWorker();

        /// <summary>
        /// Pending export requests
        /// </summary>
        public PendingExports PendingExports = new PendingExports();

        /// <summary>
        /// Host app can use this to get diagnostic reports
        /// </summary>
        public Diagnostics? Diagnostics;

        /// <summary>
        /// Affects some timings to allow integration tests to be able to run mamy clients from one browser
        /// </summary>
        private bool IntegrationTestMode;

        private bool IsInitialized;

        internal SqlFlavorizer.VendorKind DatabaseVendor;

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="connectionResolver">Host app function to get an open database connection, which will be disposed after each use</param>
        /// <param name="dbVendor"></param>
        /// <param name="ddict"></param>
        /// <param name="integrationTestMode"></param>
        /// <param name="lockDatabaseNumber"></param>
        public virtual void Initialize(SqlFlavorizer.VendorKind dbVendor, DataDictionary ddict, Func<int, Task<DbConnection>> connectionResolver,
            int lockDatabaseNumber = 0, bool integrationTestMode = false)
        {
            if (IsInitialized) throw new Exception("Already initialized");
            IsInitialized = true;
            if (!ddict.IsFinalized) throw new Exception("DataDictionary must be finalized first");
            DatabaseVendor = dbVendor;
            DataDictionary = ddict;
            GetDbConnection = connectionResolver;
            LockManager = new LockManager();
            LockManager.Initialize(() => GetDbConnection(lockDatabaseNumber));
            DefaultSql = new RetroSql();
            DefaultSql.Initialize(DatabaseVendor);
            Diagnostics = new Diagnostics(ClientPlex, DatonCache);
            IntegrationTestMode = integrationTestMode;

            //set up process to clean cache
            BackgroundWorker.Register(() =>
            {
                DatonCache.Clean(ClientPlex);
                return Task.CompletedTask;
            }, 60);

            //set up process to clean abandoned sessions
            async Task clientCleanerCallback(string sessionKey) => await LockManager.ReleaseLocksForSession(sessionKey);
            if (!integrationTestMode)
            {
                BackgroundWorker.Register(async () =>
                {
                    await ClientPlex.Clean(clientCleanerCallback);
                }, 70);
            }

            //set up process for communication with other servers about locks
            int interServerInterval = integrationTestMode ? 4 : 30; //seconds
            BackgroundWorker.Register(DoLockRefresh, interServerInterval); 
        }

        /// <summary>
        /// Clean up
        /// </summary>
        public void Dispose()
        {
            BackgroundWorker?.Dispose();
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
        /// <param name="r">the inhected implementation</param>
        public void OverrideSql(string typeName, RetroSql r)
        {
            r.Initialize(DatabaseVendor);
            SqlOverrides[typeName] = r;
        }

        /// <summary>
        /// Notify RetroDRY that the host app has changed permissions for a user; this causes the permission set to be sent to connected sessions for the user.
        /// Note that this bases the change on IUser.Id
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
            var resp = new MainResponse();
            try
            {
                var user = ClientPlex.GetUser(req.SessionKey);
                if (user == null) return new MainResponse { ErrorCode = Constants.ERRCODE_BADUSER };
                await HandleHttpMain(req, user, resp);
            }
            catch (Exception ex)
            {
                resp.ErrorCode = Constants.ERRCODE_INTERNAL;
                Diagnostics?.ReportClientCallError?.Invoke(ex.ToString());
            }
            return resp;
        }

        private async Task HandleHttpMain(MainRequest req, IUser user, MainResponse resp)
        {
            //initialize
            if (req.Initialize != null)
            {
                resp.DataDictionary = Retrovert.DataDictionaryToWire(DataDictionary, user, LanguageMessages);
            }

            if (req.SessionKey == null) throw new Exception("Missing session key");
            if (LockManager == null) throw new Exception("Uninitialized Retroverse");

            //load datons
            if (req.GetDatons != null)
            {
                var getResponses = new List<GetDatonResponse>();
                foreach (var drequest in req.GetDatons)
                {
                    var loadResult = await GetDaton(DatonKey.Parse(drequest.Key), user, forceCheckLatest: drequest.ForceLoad);
                    var getResponse = new GetDatonResponse
                    {
                        Errors = loadResult.Errors
                    };
                    if (loadResult.Daton != null) //null means it was not found by key, usually
                    {
                        if (loadResult.Daton.Key == null) 
                            throw new Exception("Expected daton key in GetDatons");
                        bool doReturnToCaller = loadResult.Daton.Version == null || drequest.KnownVersion != loadResult.Daton.Version; //omit if client already has the current version
                        if (doReturnToCaller)
                        {
                            getResponse.CondensedDaton = new CondensedDatonResponse
                            {
                                CondensedDatonJson = Retrovert.ToWire(DataDictionary, loadResult.Daton, false)
                            };
                        }
                        if (drequest.DoSubscribe && loadResult.Daton is Persiston)
                        {
                            if (loadResult.Daton.Version == null)
                                throw new Exception("Expected daton version in GetDatons");
                            ClientPlex.ManageSubscribe(req.SessionKey, loadResult.Daton.Key, loadResult.Daton.Version, true);
                        }
                    }
                    else
                        getResponse.Key = drequest.Key; //only needed if daton is not returned to client
                    getResponses.Add(getResponse);
                }
                resp.GetDatons = getResponses.ToArray();
            }

            //save datons
            if (req.SaveDatons != null)
            {
                var diffs = new List<PersistonDiff>();
                foreach (var saveRequest in req.SaveDatons)
                {
                    var diff = Retrovert.FromDiff(DataDictionary, saveRequest);
                    diffs.Add(diff);
                }
                (bool success, var results) = await SaveDatons(req.SessionKey, user, diffs.ToArray());

                var saveResponses = new List<SavePersistonResponse>();
                foreach (var result in results)
                {
                    saveResponses.Add(new SavePersistonResponse
                    {
                        IsDeleted = result.IsDeleted,
                        IsSuccess = result.IsSuccess,
                        OldKey = result.OldKey?.ToString(),
                        NewKey = result.NewKey?.ToString(),
                        NewVersion = result.NewVersion,
                        Errors = result.Errors
                    });
                }
                resp.SavedPersistons = saveResponses.ToArray();
                resp.SavePersistonsSuccess = success;
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
                    bool isSubscribed = false;
                    if (datonKey is PersistonKey)
                    {
                        ClientPlex.ManageSubscribe(req.SessionKey, datonKey, mrequest.Version, wantsSubscribe);
                        isSubscribed = wantsSubscribe;
                    }

                    //handle change in lock
                    string? lockErrorCode = "";
                    bool hasLock = false;
                    if (wantsLock)
                    {
                        if (string.IsNullOrEmpty(mrequest.Version)) throw new Exception("Version required to lock daton");
                        (hasLock, lockErrorCode) = await LockManager.RequestLock(datonKey, mrequest.Version, req.SessionKey);
                    }
                    else
                    {
                        await LockManager.ReleaseLock(datonKey, req.SessionKey);
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

            //export 1 daton
            if (req.ExportRequest != null)
            {
                if (req.ExportRequest.Format != "CSV") throw new Exception("Unknown format");
                string key = PendingExports.StoreRequest(user, DatonKey.Parse(req.ExportRequest.DatonKey), req.ExportRequest.MaxRows);
                resp.ExportRequestKey = key;
            }

            //quit - free up locks and memory
            if (req.DoQuit)
            {
                ClientPlex.DeleteSession(req.SessionKey);
                await LockManager.ReleaseLocksForSession(req.SessionKey);
            }
        }

        /// <summary>
        /// Host app must call this when receiving http request on POST /api/retro/long
        /// </summary>
        public async Task<LongResponse> HandleHttpLong(LongRequest req)
        {
            var resp = new LongResponse();
            try
            {
                var user = ClientPlex.GetUser(req.SessionKey);
                if (user == null) return new LongResponse { ErrorCode = Constants.ERRCODE_BADUSER };
                resp = await HandleHttpLong(req, user);
            }
            catch (Exception ex)
            {
                resp.ErrorCode = Constants.ERRCODE_INTERNAL;
                Diagnostics?.ReportClientCallError?.Invoke(ex.ToString());
            }
            return resp;
        }

        private async Task<LongResponse> HandleHttpLong(LongRequest req, IUser user)
        {
            if (req.SessionKey == null) throw new Exception("Missing session key");

            //if there is already something to push, then return now, without any awaiting
            var pushGroup = ClientPlex.GetAndClearItemsToPush(req.SessionKey);
            if (pushGroup != null) return PushGroupToLongResponse(user, pushGroup);

            //wait up to 30s for something to be ready to send to the client
            var pollTask = ClientPlex.BeginLongPoll(req.SessionKey);
            if (pollTask == null) return new LongResponse { ErrorCode = Constants.ERRCODE_BADUSER };
            var timeoutTask = Task.Delay(IntegrationTestMode ? 1 : 30000); 
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
        /// Get a daton, from cache or load from database. The reutrn value is a shared instance so the caller may not modify it.
        /// For new unsaved persistons with -1 as the key, this will create the instance with default values.
        /// </summary>
        /// <param name="user">if null, the return value is a shared guaranteed complete daton; if user is provided,
        /// the return value may be a clone with some rows removed or columns set to null</param>
        /// <param name="forceCheckLatest">if true then checks database to ensure latest version even if it was cached</param>
        /// <param name="key">identifies daton to get</param>
        /// <returns>object with daton, or readable errors</returns>
        public virtual async Task<RetroSql.LoadResult> GetDaton(DatonKey key, IUser? user, bool forceCheckLatest = false)
        {
            if (LockManager == null || GetDbConnection == null) throw new Exception("Uninitialized Retroverse");

            //new persiston: return now
            if (key.IsNew)
            {
                var datondef2 = DataDictionary.FindDef(key);
                Daton newDaton = Utils.ConstructDaton(datondef2.Type, datondef2);
                Utils.FixTopLevelDefaultsInNewPersiston(datondef2, newDaton);
                newDaton.Key = key;
                if (datondef2.Initializer != null) await datondef2.Initializer(newDaton);
                return new RetroSql.LoadResult { Daton = newDaton };
            }

            //get from cache if possible, and optionally ignore cached version if it is not the latest
            string? verifiedVersion = null;
            Daton? daton = DatonCache.Get(key);
            if (forceCheckLatest && daton != null)
            {
                //viewons: always ignore cache; persistons: use cached only if known to be latest
                if (daton is Persiston)
                {
                    verifiedVersion = await LockManager.GetVersion(key);
                    if (verifiedVersion != daton.Version) daton = null;
                }
                else
                    daton = null;
            }

            //get from database if needed (and cache it), or abort
            var datondef = DataDictionary.FindDef(key);
            if (typeof(Persiston).IsAssignableFrom(datondef.Type) && (key is ViewonKey)) throw new Exception("Persiston requested but key format is for viewon");
            if (typeof(Viewon).IsAssignableFrom(datondef.Type) && (key is PersistonKey)) throw new Exception("Viewon requested but key format is for persiston");
            if (daton == null)
            {
                var sql = GetSqlInstance(key);
                if (sql == null) throw new Exception("Cannot resolve RetroSql instance in GetDaton");
                RetroSql.LoadResult? loadResult;
                using (var db = await GetDbConnection(datondef.DatabaseNumber))
                    loadResult = await sql.Load(db, DataDictionary, user, key, ViewonPageSize);
                if (loadResult == null)
                    return new RetroSql.LoadResult { Errors = new[] { Constants.ERRCODE_NOTFOUND } };
                if (loadResult.Daton == null) return loadResult;
                daton = loadResult.Daton;
                if (verifiedVersion == null && daton is Persiston)
                    verifiedVersion = await LockManager.GetVersion(key);
                daton.Version = verifiedVersion;
                DatonCache.Put(daton);
                Diagnostics?.IncrementLoadCount();
            }

            //enforce permissions on the user
            if (user != null)
            {
                daton = daton.Clone(datondef);
                var guard = new SecurityGuard(DataDictionary, user);
                guard.HidePrivateParts(daton);
            }

            return new RetroSql.LoadResult { Daton = daton };
        }

        /// <summary>
        /// Host app must call this when receiving http request on GET /api/retro/export;
        /// adds exported data to the given stream
        /// </summary>
        /// <param name="requestKey">as generated in HandleHttpMain</param>
        public IActionResult HandleHttpExport(string requestKey)
        {
            return new FileCallbackResult(new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain"), async (outputStream, _) =>
            {
                await using var wri = new StreamWriter(outputStream);
                await HandleHttpExport(wri, requestKey);
            });
        }

        /// <summary>
        /// Called from the public overload
        /// </summary>
        /// <param name="responseWriter">a writer which this method will only write async to, and leave open</param>
        /// <param name="requestKey">as generated in HandleHttpMain</param>
        async Task HandleHttpExport(StreamWriter responseWriter, string requestKey)
        {
            if (GetDbConnection == null) throw new Exception("Uninitialized Retroverse");

            //get info about what to export
            (IUser? user, DatonKey datonKey, int maxRows) = PendingExports.RetrieveRequest(requestKey);
            maxRows = Math.Min(maxRows, MaxExportRows);

            //initailize resolver for lookup values
            var lookupResolver = new LookupResolver(async datonKey =>
            {
                var result = await GetDaton(datonKey, user);
                if (result.Daton == null) return (null, null);
                return (DataDictionary.FindDef(result.Daton), result.Daton);
            });

            try
            {
                //if viewon, use specialized technique for large result sets
                if ((datonKey is ViewonKey viewonKey) && viewonKey.PageNumber == 0)
                {
                    var datondef = DataDictionary.FindDef(viewonKey);
                    var sql = GetSqlInstance(viewonKey);
                    using var db = await GetDbConnection(datondef.DatabaseNumber);
                    if (sql == null) throw new Exception("Cannot resolve RetroSql instance in HandleHttpExport");

                    var csvConverter = new CsvConverter(DataDictionary, lookupResolver, datondef);
                    await csvConverter.WriteHeaderRow(responseWriter);
                    int rowNo = 0;
                    await foreach (var row in sql.LoadForExport(db, DataDictionary, user, viewonKey, maxRows))
                    {
                        if (row == null) continue;
                        await csvConverter.WriteRow(row, ++rowNo, responseWriter);
                    }
                }

                //in all other cases load in memory then stream out
                else
                {
                    var result = await GetDaton(datonKey, user);
                    if (result.Daton != null)
                    {
                        var datondef = DataDictionary.FindDef(result.Daton);
                        var csvConverter = new CsvConverter(DataDictionary, lookupResolver, datondef);
                        await csvConverter.WriteHeaderRow(responseWriter);
                        await csvConverter.WriteAllRows(result.Daton, responseWriter);
                    }
                }
            }
            catch (Exception ex)
            {
                Diagnostics?.ReportClientCallError?.Invoke(ex.ToString());
                await responseWriter.WriteLineAsync($"Internal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Save one or more persiston changes. This will clear those items from cache, and they will have newly assigned version numbers.
        /// Propogation of changes does not happen until the lock is released (not here).
        /// </summary>
        public virtual async Task<(bool, MultiSaver.Result[])> SaveDatons(string sessionKey, IUser user, PersistonDiff[] diffs)
        {
            if (LockManager == null) throw new Exception("Uninitialized Retroverse");

            //confirm this user has locks
            var keysAndVersions = diffs.Select(d => (d.Key, d.BasedOnVersion));
            var lockError = ConfirmAllLocks(sessionKey, keysAndVersions);
            if (lockError != null)
                return (false, new MultiSaver.Result[] { lockError });

            //save
            using var saver = new MultiSaver(this, user, diffs);
            bool success = await saver.Save();
            var saverResults = saver.GetResults();

            //clean cache/locks, assign version numbers, and push changes to other clients
            if (success)
            {
                foreach (var result in saverResults)
                {
                    if (result.NewKey != null)
                    {
                        (bool verOk, string? newVersion) = await LockManager.AssignNewVersion(result.NewKey, sessionKey);
                        result.NewVersion = newVersion;
                        DatonCache.Remove(result.NewKey);

                        //propagation may reload and therefore re-cache
                        if (ClientPlex.IsAnyOutOfDate(result.NewKey, result.NewVersion))
                        {
                            var daton = (await GetDaton(result.NewKey, null, forceCheckLatest: true))?.Daton;
                            if (daton != null)
                                ClientPlex.NotifyClientsOf(DataDictionary, new[] { daton });
                        }
                    }
                }
            }

            return (success, saverResults);
        }

        /// <summary>
        /// Check whether the user holds locks on the given datons. If yes, returns null, else returns
        /// an error structure that can be returned to a caller, noting ONLY the first encountered problem.
        /// </summary>
        public virtual MultiSaver.Result? ConfirmAllLocks(string sessionKey, IEnumerable<(DatonKey, string?)> datonKeysAndVersions)
        {
            if (LockManager == null) throw new Exception("Uninitialized Retroverse");

            //Note: The client would already know what locks they have so this would be an 
            //unusual error. 
            foreach ((var datonKey, string? reqVersion) in datonKeysAndVersions)
            {
                if (datonKey.IsNew) continue;
                (_, bool isLockedByMe, string? actualVersion) = LockManager.GetLockState(datonKey, sessionKey);
                if (!isLockedByMe)
                    return new MultiSaver.Result { OldKey = datonKey, Errors = new[] { Constants.ERRCODE_LOCK } };
                if (reqVersion != actualVersion)
                    return new MultiSaver.Result { OldKey = datonKey, Errors = new[] { Constants.ERRCODE_VERSION } };
            }
            return null;
        }

        /// <summary>
        /// Get the RetroSql instance to use to load/save a daton
        /// </summary>
        public virtual RetroSql? GetSqlInstance(DatonKey key)
        {
            if (SqlOverrides.TryGetValue(key.Name, out RetroSql r)) return r;
            return DefaultSql;
        }

        /// <summary>
        /// Create a new session. The session will last until the client quits cleanly or stops sending long polling requests.
        /// </summary>
        /// <param name="user">This includes the security roles that RetroDRY will enforce</param>
        /// <returns>a session key which must be registered client side</returns>
        public virtual string CreateSession(IUser user)
        {
            return ClientPlex.CreateSession(user);
        }

        /// <summary>
        /// For integration testing only; clean out clients that haven't been accessed in 10 seconds
        /// </summary>
        public async Task DiagnosticCleanup()
        {
            if (LockManager == null) throw new Exception("Uninitialized Retroverse");

            DatonCache.Clean(ClientPlex, secondsOld: 10);
            async Task clientCleanerCallback(string sessionKey) => await LockManager.ReleaseLocksForSession(sessionKey);
            await ClientPlex.Clean(clientCleanerCallback, secondsOld: 10);
        }

        /// <summary>
        /// Restructure a ClientPlex.PushGroup into a LongResponse for sending via http
        /// </summary>
        private LongResponse PushGroupToLongResponse(IUser user, ClientPlex.PushGroup pg)
        {
            var wireDatons = new List<CondensedDatonResponse>();
            if (pg.Datons != null)
            {
                foreach (var daton in pg.Datons)
                    wireDatons.Add(new CondensedDatonResponse
                    {
                        CondensedDatonJson = Retrovert.ToWire(DataDictionary, daton, false)
                    });
            }
            var response = new LongResponse
            {
                CondensedDatons = wireDatons.ToArray(),
            };

            //if permissions changed, resend the whole data dictionary
            if (pg.IncludeDataDictionary)
                response.DataDictionary = Retrovert.DataDictionaryToWire(DataDictionary, user, LanguageMessages);
            
            return response;
        }

        /// <summary>
        /// Called periodically to touch locked records (communicating to other servers) and fetch changes
        /// (receiving from other servers) to multiplex to all subscribed clients
        /// </summary>
        protected async virtual Task DoLockRefresh()
        {
            if (LockManager == null) throw new Exception("Uninitialized Retroverse");

            List<(DatonKey, string?)> otherServerUpdates = await LockManager.InterServerProcess();
            var updatesWithSubscriptions = otherServerUpdates.Where(pair => ClientPlex.IsAnyOutOfDate(pair.Item1, pair.Item2)).ToArray();
            var datonsToPush = new List<Daton>(updatesWithSubscriptions.Length);
            foreach (var pair in updatesWithSubscriptions)
            {
                var daton = (await GetDaton(pair.Item1, null, forceCheckLatest: true))?.Daton;
                if (daton != null)
                    datonsToPush.Add(daton);
            }
            if (datonsToPush.Any())
                ClientPlex.NotifyClientsOf(DataDictionary, datonsToPush.ToArray());
        }
    }
}
