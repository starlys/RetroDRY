using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Subclassable default behavior for loading and saving datons
    /// </summary>
    public partial class RetroSql
    {
        /// <summary>
        /// Used in Save to hold values for each recursion level
        /// </summary>
        protected class TraversalData
        {
            public object ParentKey;
            public TableDef TableDef;
            public List<PersistonDiff.DiffRow> DiffRowList;
            public IList PristineList; //null if single main row or there was no old row
            public IList ModifiedList; //null if single main row or there is no new row

            /// <summary>
            /// return values are object (newly assigned key for the row) and bool (true to process child tables)
            /// </summary>
            public Func<RowChangingData, Task<(object, bool)>> ProcessRowF;
        }

        /// <summary>
        /// Used in sevaral data saving methods to identify everything needed for the SQL calls
        /// </summary>
        protected class RowChangingData
        {
            /// <summary>
            /// primary key of parent row (use to set parent key on new rows)
            /// </summary>
            public object ParentKey;

            public TableDef TableDef;

            /// <summary>
            /// Container of DiffRow (unlikely to be needed)
            /// </summary>
            public List<PersistonDiff.DiffRow> DiffRowList;

            /// <summary>
            /// Info about what change to make for this row
            /// </summary>
            public PersistonDiff.DiffRow DiffRow;

            /// <summary>
            /// The original row (null if DiffRow indicates a new row)
            /// </summary>
            public Row PristineRow;
            
            /// <summary>
            /// The modified row (null if DiffRow indicates a deleted row)
            /// </summary>
            public Row ModifiedRow;
        }

        /// <summary>
        /// Save persiston to database
        /// </summary>
        /// <param name="pristineDaton">null or the version before the diff was applied</param>
        /// <param name="modifiedDaton">the validated final version</param>
        /// <param name="diff">the difference between pristine and modified, which is what this method inspects to make the changes</param>
        public virtual async Task Save(IDbConnection db, Persiston pristineDaton, Persiston modifiedDaton, PersistonDiff diff)
        {
            //called for each row in traversal; return true to recurse over children
            async Task<(object, bool)> rowCallback(RowChangingData cdata)
            {
                if (cdata.DiffRow.Kind == DiffKind.DeletedRow)
                {
                    await DeleteRowWithCascade(db, cdata.TableDef, cdata.PristineRow);
                    return (null, false); //don't recur to children of deleted row
                }
                object newpk = await InsertUpdateRow(db, cdata);
                return (newpk, true);
            }

            var tdata = new TraversalData
            {
                ParentKey = null,
                TableDef = diff.DatonDef.MainTableDef,
                DiffRowList = diff.MainTable,
                PristineList = null,
                ModifiedList = null,
                ProcessRowF = rowCallback
            };
            if (diff.DatonDef.MultipleMainRows)
            {
                var mainListField = tdata.TableDef.RowType.GetField(tdata.TableDef.Name);
                tdata.PristineList = mainListField.GetValue(pristineDaton) as IList;
                tdata.ModifiedList = mainListField.GetValue(modifiedDaton) as IList;
            }    
            await TraverseDiffList(tdata);
        }

        /// <summary>
        /// Call ProcessRowF on all rows in the diff list, and recurse to all child tables. 
        /// </summary>
        protected async Task TraverseDiffList(TraversalData tdata)
        {
            var pkField = tdata.TableDef.RowType.GetField(tdata.TableDef.PrimaryKeyColName);

            foreach (var row in tdata.DiffRowList)
            {
                if (!row.Columns.TryGetValue(tdata.TableDef.PrimaryKeyColName, out object rowKey))
                    throw new Exception($"Diff row is missing primary key {tdata.TableDef.PrimaryKeyColName}");

                //find pristine row
                Row pristineRow = null;
                if (tdata.PristineList != null && row.Kind != DiffKind.NewRow)
                {
                    int rowIdx = Utils.IndexOfPrimaryKeyMatch(tdata.PristineList, pkField, rowKey);
                    if (rowIdx >= 0) pristineRow = tdata.PristineList[rowIdx] as Row;
                }

                //find modified row 
                Row modifiedRow = null;
                if (tdata.ModifiedList != null)
                {
                    int rowIdx = Utils.IndexOfPrimaryKeyMatch(tdata.ModifiedList, pkField, rowKey);
                    if (rowIdx >= 0) modifiedRow = tdata.ModifiedList[rowIdx] as Row;
                }

                //callback to process row
                var cdata = new RowChangingData
                {
                    ParentKey = tdata.ParentKey,
                    DiffRow = row,
                    DiffRowList = tdata.DiffRowList,
                    TableDef = tdata.TableDef,
                    PristineRow = pristineRow,
                    ModifiedRow = modifiedRow
                };
                (object newPK, bool doTraverseChildren) = await tdata.ProcessRowF?.Invoke(cdata);
                if (newPK != null) rowKey = newPK;

                //skip child tables?
                if (!doTraverseChildren) continue;

                //loop child tables of this row
                if (row.ChildTables != null)
                {
                    foreach (var child in row.ChildTables)
                    {
                        var childTableDef = child.Key;
                        var childRows = child.Value;
                        var listField = childTableDef.RowType.GetField(childTableDef.Name);

                        //get pristine list
                        IList childPristineList = null;
                        if (pristineRow != null)
                            childPristineList = listField.GetValue(pristineRow) as IList;

                        //get modified list
                        IList childModifiedList = null;
                        if (modifiedRow != null)
                            childModifiedList = listField.GetValue(modifiedRow) as IList;

                        var childTdata = new TraversalData
                        {
                            ParentKey = rowKey,
                            TableDef = childTableDef,
                            DiffRowList = childRows,
                            PristineList = childPristineList,
                            ModifiedList = childModifiedList,
                            ProcessRowF = tdata.ProcessRowF
                        };
                        await TraverseDiffList(childTdata);
                    }
                }
            }
        }
    }
}
