﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Manager of locks held by all sessions. Also see RetroLock for SQL layer used by this class.
    /// </summary>
    internal class LockManager
    {
        /// <summary>
        /// An item that is locked 
        /// </summary>
        internal class LockInfo
        {
            public string SessionKey;
            public DatonKey DatonKey;

            /// <summary>
            /// The version at the time of the lock, which may be updated on save
            /// </summary>
            public string LockVersion;

            public LockInfo(string sessionKey, DatonKey datonKey, string oldVersion)
            {
                SessionKey = sessionKey;
                DatonKey = datonKey;
                LockVersion = oldVersion;
            }
        }

        /// <summary>
        /// A random number indicating this server lifetime (see RetroLock table)
        /// </summary>
        private int ServerLifeNumber;

        /// <summary>
        /// Locks owned by this process, across all users
        /// </summary>
        private readonly ConcurrentDictionary<DatonKey, LockInfo> Locks = new ConcurrentDictionary<DatonKey, LockInfo>();

        private Func<Task<DbConnection>>? GetLockConnection;

        private DateTime LastCheckedOtherServers = DateTime.UtcNow;

        private DateTime NextCleanup = DateTime.UtcNow.AddHours(1);

        /// <param name="lockDatabaseConnection">function that returns the database connection</param>
        public void Initialize(Func<Task<DbConnection>> lockDatabaseConnection)
        {
            GetLockConnection = lockDatabaseConnection;
            ServerLifeNumber = (new Random().Next(10000, 99999));
        }

        /// <summary>
        /// Get the state of a daton lock. This only checks the local server, so the daton could be locked even if it returns
        /// a value indicating not locked.
        /// </summary>
        /// <returns>3 values: is locked at all; is locked by the given session; version number when locked</returns>
        public (bool, bool, string?) GetLockState(DatonKey datonKey, string sessionKey)
        {
            if (Locks.TryGetValue(datonKey, out LockInfo linfo))
            {
                bool isLockedByMe = linfo.SessionKey == sessionKey;
                return (true, isLockedByMe, linfo.LockVersion);
            }
            return (false, false, null);
        }

        /// <summary>
        /// Get the version of the persiston. This ALWAYS goes to the database so don't call this too much.
        /// The result is guaranteed and will assign a version if there was none recorded.
        /// </summary>
        public async Task<string> GetVersion(DatonKey datonKey)
        {
            if (GetLockConnection == null) throw new Exception("Uninitialized LockManager");
            using var lockdb = await GetLockConnection();
            (string version, _) = RetroLock.GetVersion(lockdb, datonKey);
            return version;
        }

        /// <summary>
        /// Attempt to get a lock for updating a persiston. Fails if another user has it locked already. 
        /// Succeeds efficiently if this user already had it locked.
        /// </summary>
        /// <returns>success flag and error reason code</returns>
        public async Task<(bool, string?)> RequestLock(DatonKey datonKey, string version, string sessionKey)
        {
            if (GetLockConnection == null) throw new Exception("Uninitialized LockManager");

            //check if already locked by this server
            if (Locks.TryGetValue(datonKey, out LockInfo linfo))
            {
                bool isLockedByMe = linfo.SessionKey == sessionKey;
                if (!isLockedByMe) return (false, Constants.ERRCODE_LOCK); //someone else on this server has it locked
                return (true, null); //this session already has it locked
            }

            //attempt lock on database
            using var lockdb = await GetLockConnection();
            if (RetroLock.Lock(lockdb, datonKey, version, sessionKey))
            {
                //success
                Locks[datonKey] = new LockInfo(sessionKey, datonKey, version);
                return (true, null);
            }

            //failed, so determine why
            (string verifiedVersion, _) = RetroLock.GetVersion(lockdb, datonKey);
            if (verifiedVersion != version)
                return (false, Constants.ERRCODE_VERSION); //the most recent version is newer than the version known by the caller
            return (false, Constants.ERRCODE_LOCK); //someone else on another server has it locked
        }

        /// <summary>
        /// Assigns a new version and keeps a persiston locked. Call this after a successful save.
        /// </summary>
        /// <returns>success flag and the new version</returns>
        public async Task<(bool, string?)> AssignNewVersion(DatonKey datonKey, string sessionKey)
        {
            if (GetLockConnection == null) throw new Exception("Uninitialized LockManager");
            if (!Locks.TryGetValue(datonKey, out LockInfo linfo)) return (false, null);
            using var lockdb = await GetLockConnection();
            (bool success, string? newVersion) = RetroLock.AssgnNewVersion(lockdb, datonKey, sessionKey, ServerLifeNumber);
            if (success && newVersion != null) linfo.LockVersion = newVersion;

            return (success, newVersion);
        }

        /// <summary>
        /// Attempt to release a lock
        /// </summary>
        /// <returns>success flag</returns>
        public async Task<bool> ReleaseLock(DatonKey datonKey, string sessionKey)
        {
            if (GetLockConnection == null) throw new Exception("Uninitialized LockManager");

            //check if it is possible to unlock
            if (Locks.TryGetValue(datonKey, out LockInfo linfo))
            {
                bool isLockedByMe = linfo.SessionKey == sessionKey;
                if (!isLockedByMe) return false; //can't unlock because someone else on this server has it locked
            }
            else return false; //can't unlock because it is not locked by anyone on this server

            //attempt unlock on database
            using var lockdb = await GetLockConnection();
            bool unlockOK = RetroLock.Unlock(lockdb, datonKey, sessionKey, ServerLifeNumber);
            Locks.TryRemove(datonKey, out _); //we forget in memory even if there was a database problem (should not happen)

            return unlockOK;
        }

        /// <summary>
        /// Release all locks held by a sesson. This is rare so does not have to be efficient.
        /// </summary>
        /// <param name="sessionKey"></param>
        public async Task ReleaseLocksForSession(string sessionKey)
        {
            var linfos = Locks.Values.Where(i => i.SessionKey == sessionKey).ToArray();
            foreach (var linfo in linfos)
                await ReleaseLock(linfo.DatonKey, sessionKey);
        }

        /// <summary>
        /// Update the database touched time for all current locks, so that other servers know that this server has not died.
        /// Then also obtain changes from other servers since last checked and return the new key and versions.
        /// Occasionally this also cleans out the lock table of old entries.
        /// </summary>
        public async Task<List<(DatonKey, string?)>> InterServerProcess()
        {
            if (GetLockConnection == null) throw new Exception("Uninitialized LockManager");

            var sinceUtc = LastCheckedOtherServers.AddSeconds(-5); //a little overlap
            LastCheckedOtherServers = DateTime.UtcNow;
            using var lockdb = await GetLockConnection();

            //touch records that are still locked
            foreach (var linfo in Locks.Values)
                RetroLock.Touch(lockdb, linfo.DatonKey, linfo.SessionKey);

            //cleanup every 12 hours
            if (DateTime.UtcNow > NextCleanup)
            {
                NextCleanup = DateTime.UtcNow.AddHours(12);
                RetroLock.Cleanup(lockdb);
            }

            //read from other servers
            return RetroLock.GetRecentUpdatesByOtherServers(lockdb, sinceUtc, ServerLifeNumber);
        }
    }
}
