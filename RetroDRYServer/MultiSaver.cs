using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Encapsulation of validation, database transactions and saving for persiston saving. In particular it will roll back
    /// transactions on more than one database if any save fails.
    /// This must be short-lived and disposed of since it holds database connections.
    /// </summary>
    /// <remarks>
    /// This is a friend class to Retroverse, as functions in both classes call the other class. 
    /// It assumes the caller deals with locks.
    /// </remarks>
    public class MultiSaver : IDisposable
    {
        /// <summary>
        /// Result data from a save operation
        /// </summary>
        public class Result
        {
            /// <summary>
            /// The original key, which may indicate a new unsaved persiston
            /// </summary>
            public DatonKey OldKey;

            /// <summary>
            /// This key will be different from the requested key for new persistons; may be null on error
            /// </summary>
            public DatonKey NewKey;

            /// <summary>
            /// Collection of error messages if save was not successful
            /// </summary>
            public string[] Errors;

            /// <summary>
            /// True if save was successful
            /// </summary>
            public bool IsSuccess;

            /// <summary>
            /// True if entire persison was deleted
            /// </summary>
            public bool IsDeleted;
        }

        private class SaveItem
        {
            public PersistonDiff Diff;
            public Persiston Pristine, Modified;
            public DatonDef DatonDef;
            public List<string> Errors = new List<string>();

            /// <summary>
            /// Is true when the diff specifies that all main rows are deleted
            /// </summary>
            public bool IsDeleted;
        }

        private class Trx
        {
            public int DatabaseNumber;
            public DbConnection Connection;
            public DbTransaction Transaction;
        }

        private readonly Retroverse Retroverse;
        private readonly SecurityGuard Guard;
        private readonly IUser User;
        private readonly SaveItem[] SaveItems;
        private readonly List<Trx> Trxs = new List<Trx>();

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="retroverse"></param>
        /// <param name="user"></param>
        /// <param name="diffs">all persistons that need to be saved</param>
        public MultiSaver(Retroverse retroverse, IUser user, PersistonDiff[] diffs)
        {
            Retroverse = retroverse;
            User = user;
            Guard = new SecurityGuard(retroverse.DataDictionary, user);
            SaveItems = diffs.Select(d => new SaveItem { Diff = d }).ToArray();
        }

        /// <summary>
        /// Clean up
        /// </summary>
        public void Dispose()
        {
            foreach (var trx in Trxs)
            {
                trx.Transaction?.Dispose();
                trx.Connection.Dispose();
            }
            Trxs.Clear();
        }

        /// <summary>
        /// Attempt saving
        /// </summary>
        /// <returns>true if success</returns>
        public async Task<bool> Save()
        {
            //prep pristine and modified, validate
            bool anyFailed = false;
            foreach (var i in SaveItems)
            {
                bool prepOK = await Prep(i);
                if (!prepOK) anyFailed = true;
            }
            if (anyFailed) return false;

            //start transactions 
            var databaseNumbers = SaveItems.Select(i => i.DatonDef.DatabaseNumber).Distinct().ToArray();
            foreach (var dbno in databaseNumbers)
            {
                var db = await Retroverse.GetDbConnection(dbno);
                var trx = new Trx { DatabaseNumber = dbno, Connection = db };
                Trxs.Add(trx); //being careful to add disposable objects to the class-level data in a way that allows it to be disposed if this method fails
                var dbtrx = db.BeginTransaction();
                trx.Transaction = dbtrx;
            }

            //save or abort
            anyFailed = false;
            foreach (var i in SaveItems)
            {
                bool saveOK = await SaveOne(i);
                if (!saveOK) { anyFailed = true; break; }

                //for caller convenience, determine if the main row(s) are all deleted
                i.IsDeleted = i.Diff.MainTable.All(r => r.Kind == DiffKind.DeletedRow);
            }

            //end transactions
            foreach (var trx in Trxs)
            {
                if (anyFailed) trx.Transaction.Rollback();
                else trx.Transaction.Commit();
            }

            //update locked flags
            if (!anyFailed)
            {
                foreach (var i in SaveItems)
                    Retroverse.LockManager.NotifyDatonWritten(i.Diff.Key);
            }

            return !anyFailed;
        }

        /// <summary>
        /// Get results detail
        /// </summary>
        public Result[] GetResults()
        {
            return SaveItems.Select(item => new Result
            {
                OldKey = item.Diff.Key, 
                NewKey = item.Modified.Key, 
                IsSuccess = item.Errors.Count == 0,
                Errors = item.Errors.ToArray(),
                IsDeleted = item.IsDeleted 
            }).ToArray();
        }

        /// <summary>
        /// Set up Pristine/Modified, DatonDef, and handle permissions and validation
        /// </summary>
        /// <returns>true if ok</returns>
        private async Task<bool> Prep(SaveItem item)
        {
            item.DatonDef = Retroverse.DataDictionary.FindDef(item.Diff.Key);

            //security check
            var securityErrors = Guard.GetDisallowedWrites(item.Pristine, item.DatonDef, item.Diff);
            item.Errors.AddRange(securityErrors);

            //get pristine, diff, and modified versions
            if (item.Diff.Key.IsNew)
            {
                item.Modified = Utils.ConstructDaton(item.DatonDef.Type, item.DatonDef) as Persiston; 
                item.Modified.Key = item.Diff.Key;
            }
            else
            {
                item.Pristine = (await Retroverse.GetDaton(item.Diff.Key, null))?.Daton as Persiston;
                item.Modified = item.Pristine.Clone(item.DatonDef) as Persiston;
            }
            item.Diff.ApplyTo(item.DatonDef, item.Modified);

            //validate Modified
            var validator = new Validator(User);
            await validator.ValidatePersiston(item.DatonDef, item.Modified);
            item.Errors.AddRange(validator.Errors);
            return item.Errors.Count == 0;
        }

        /// <summary>
        /// Save one persiston and convert thrown exception to item error;
        /// also sets changed daton key if persiston was new.
        /// </summary>
        /// <returns>true on success</returns>
        private async Task<bool> SaveOne(SaveItem item)
        {
            var trx = Trxs.Single(t => t.DatabaseNumber == item.DatonDef.DatabaseNumber);
            var sql = Retroverse.GetSqlInstance(item.Modified.Key);
            try
            {
                await sql.Save(trx.Connection, User, item.Pristine, item.Modified, item.Diff);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (Retroverse.CleanUpSaveException != null) msg = Retroverse.CleanUpSaveException(User, ex);
                item.Errors.Add(msg);
            }
            AssignPersistonKey(item);

            return item.Errors.Count == 0;
        }

        /// <summary>
        /// if persiston was new, figure out new main row's key and set Modified.Key
        /// </summary>
        private static void AssignPersistonKey(SaveItem item)
        {
            if (!item.Modified.Key.IsNew) return;
            var mainTdef = item.DatonDef.MainTableDef;
            var r = RecurPoint.FromDaton(item.DatonDef, item.Modified);
            if (r is RowRecurPoint rr)
            {
                object pk = mainTdef.RowType.GetField(mainTdef.PrimaryKeyColName).GetValue(rr.Row);
                var pkdef = mainTdef.Cols.Single(c => c.Name == mainTdef.PrimaryKeyColName);
                string pkString = Retrovert.FormatRawJsonValue(pkdef, pk);
                item.Modified.Key = new PersistonKey(item.Modified.Key.Name, pkString, false);
                return;
            }

            //if reached here, client semantics was unexpected: it should have sent a new persiston only for a daton type
            //that defines a single main row; in all other cases the client should have loaded then modified an existing
            //persiston, even if it had no rows in it.
            throw new Exception("Cannot save a whole-table persiston with a daton-key identifying a primary key");
        }
    }
}
