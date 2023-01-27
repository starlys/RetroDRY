using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;

namespace RetroDRY
{
    /// <summary>
    /// Utility methods for accessing RetroLock table. This is designed to be low level and only called by LockManager,
    /// which is the class implementing business logic for locks.
    /// </summary>
    public static class RetroLock
    {
        /// <summary>
        /// Get the daton version code and session key holding the lock if any; if the lock row was missing, create it and assign
        /// the version
        /// </summary>
        /// <returns>(version,lockedBy) where lockedBy might be null</returns>
        public static (string, string?) GetVersion(DbConnection db, DatonKey key)
        {
            retry:

            //read existing row
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "select DatonVersion,LockedBy from RetroLock where DatonKey=@k";
                var p = cmd.CreateParameter();
                p.ParameterName = "k";
                p.Value = key.ToString();
                cmd.Parameters.Add(p);
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                    return (Utils.ReadString(rdr, 0)!, Utils.ReadString(rdr, 1));
            }

            //not found, so create it
            string version = Guid.NewGuid().ToString();
            using (var cmd = db.CreateCommand())
            {
                try
                {
                    cmd.CommandText = "insert into RetroLock (DatonKey,DatonVersion,Touched) values(@k,@v,@t)";
                    Utils.AddParameterWithValue(cmd, "k", key.ToString());
                    Utils.AddParameterWithValue(cmd, "v", version);
                    Utils.AddParameterWithValue(cmd, "t", DateTime.UtcNow);
                    cmd.ExecuteNonQuery();
                    return (version, null);
                }
                catch
                {
                    //rare failure: another user created the row between when we queried and attempted the insert
                    Thread.Sleep(1);
                    goto retry;
                }
            }
        }

        /// <summary>
        /// Attempt to obtain a lock
        /// </summary>
        /// <param name="version">the version which was known</param>
        /// <param name="sessionKey"></param>
        /// <param name="db"></param>
        /// <param name="key">daton key to lock</param>
        /// <returns>true if successful</returns>
        public static bool Lock(DbConnection db, DatonKey key, string version, string sessionKey)
        {
            //assuming first there is a record, attempt to get the lock by updating it 
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "update RetroLock set Touched=@t, LockedBy=@s where DatonKey=@k and DatonVersion=@v and (LockedBy is null or Touched<@old)";
                Utils.AddParameterWithValue(cmd, "t", DateTime.UtcNow);
                Utils.AddParameterWithValue(cmd, "s", sessionKey);
                Utils.AddParameterWithValue(cmd, "k", key.ToString());
                Utils.AddParameterWithValue(cmd, "v", version);
                Utils.AddParameterWithValue(cmd, "old", DateTime.UtcNow.AddSeconds(-120));
                int nrows = cmd.ExecuteNonQuery();
                if (nrows == 1) return true;
            }

            //update touched date: this tells us if the record exists, and ensures it won't be cleaned up during the lock process;
            //also unlock it if the lock is too old
            //bool recordExists;
            //using (var cmd = db.CreateCommand())
            //{
            //    cmd.CommandText = "update RetroLock set LockedBy=(case when Touched<@old then null else LockedBy end), Touched=@t where DatonKey=@k";
            //    Utils.AddParameterWithValue(cmd, "old", DateTime.UtcNow.AddSeconds(-120));
            //    Utils.AddParameterWithValue(cmd, "t", DateTime.UtcNow);
            //    Utils.AddParameterWithValue(cmd, "k", key.ToString());
            //    int nrows = cmd.ExecuteNonQuery();
            //    recordExists = nrows == 1;
            //}

            //reached here, so there was no record; create one with lock 
            //(this won't occur if everything is working, since reading the version should happen before attempting to lock)
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "insert into RetroLock (DatonKey,DatonVersion,Touched,LockedBy) values(@k,@v,@t,@s)";
                Utils.AddParameterWithValue(cmd, "k", key.ToString());
                Utils.AddParameterWithValue(cmd, "v", version);
                Utils.AddParameterWithValue(cmd, "t", DateTime.UtcNow);
                Utils.AddParameterWithValue(cmd, "s", sessionKey);
                try
                {
                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch
                {
                    return false; //another user created the lock record since we queried it above
                }
            }

        }

        /// <summary>
        /// Unlock a daton and optionally assign new version; only has an effect if it was locked by the given session
        /// </summary>
        /// <param name="datonWasWritten">pass true if this unlock is following a write, or false if it is an abandoned lock</param>
        /// <param name="db"></param>
        /// <param name="key">identifies daton to unlock</param>
        /// <param name="sessionKey">identifies session that owns the lock</param>
        /// <param name="serverLifeNumber">see RetroLock table</param>
        /// <returns>success flag and the new version (version only returned if datonWasWritten)</returns>
        public static (bool, string?) Unlock(DbConnection db, DatonKey key, string sessionKey, bool datonWasWritten, int serverLifeNumber)
        {
            string? version = datonWasWritten ? Guid.NewGuid().ToString() : null;
            using var cmd = db.CreateCommand();
            string versionsql = datonWasWritten ? ",DatonVersion=@v,UpdatedByServer=@u" : "";
            cmd.CommandText = $"update RetroLock set LockedBy=null,Touched=@t{versionsql} where DatonKey=@k and LockedBy=@s";
            Utils.AddParameterWithValue(cmd, "t", DateTime.UtcNow);
            Utils.AddParameterWithValue(cmd, "k", key.ToString());
            Utils.AddParameterWithValue(cmd, "s", sessionKey);
            if (datonWasWritten)
            {
                Utils.AddParameterWithValue(cmd, "v", version);
                Utils.AddParameterWithValue(cmd, "u", serverLifeNumber);
            }
            int nrows = cmd.ExecuteNonQuery();
            bool success = nrows == 1;
            return (success, version);
        }

        /// <summary>
        /// Get the list of daton keys with version numbers that were updated by other servers since the given time
        /// </summary>
        /// <param name="serverLifeNumber">the number for this server; updates from this server are excluded from the query</param>
        /// <param name="db"></param>
        /// <param name="sinceUtc"></param>
        public static List<(DatonKey, string?)> GetRecentUpdatesByOtherServers(DbConnection db, DateTime sinceUtc, int serverLifeNumber)
        {
            var ret = new List<(DatonKey, string?)>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = $"select DatonKey,DatonVersion from RetroLock where Touched>@t and UpdatedByServer<>@u";
                Utils.AddParameterWithValue(cmd, "t", sinceUtc);
                Utils.AddParameterWithValue(cmd, "u", serverLifeNumber);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var key = DatonKey.Parse(Utils.ReadString(rdr, 0));
                    string? version = Utils.ReadString(rdr, 1);
                    ret.Add((key, version));
                }
            }
            return ret;
        }

        /// <summary>
        /// Update the touched column for the daton, only if locked by the given session.
        /// </summary>
        public static void Touch(DbConnection db, DatonKey key, string sessionKey)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "update RetroLock set Touched=@t where DatonKey=@k and LockedBy=@s";
            Utils.AddParameterWithValue(cmd, "t", DateTime.UtcNow);
            Utils.AddParameterWithValue(cmd, "k", key.ToString());
            Utils.AddParameterWithValue(cmd, "s", sessionKey);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Delete lock records older than 5 days
        /// </summary>
        internal static void Cleanup(DbConnection db)
        {
            DateTime old = DateTime.UtcNow.AddDays(-5);
            using var cmd = db.CreateCommand();
            cmd.CommandText = "delete from RetroLock where Touched<@t";
            Utils.AddParameterWithValue(cmd, "t", old);
            cmd.ExecuteNonQuery();
        }
    }
}
