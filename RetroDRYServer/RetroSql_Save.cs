﻿using System;
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
            /// <summary>
            /// Key value of parent row
            /// </summary>
            public object? ParentKey;

            /// <summary>
            /// defintion of the table
            /// </summary>
            public TableDef TableDef;

            /// <summary>
            /// List of changes for each row
            /// </summary>
            public List<PersistonDiff.DiffRow> DiffRowList;

            /// <summary>
            /// List of unchanged rows; null if single main row or there was no old row
            /// </summary>
            public IList? PristineList;

            /// <summary>
            /// List of rows after change; null if single main row or there is no new row
            /// </summary>
            public IList? ModifiedList;

            /// <summary>
            /// return values are object (newly assigned key for the row) and bool (true to process child tables)
            /// </summary>
            public Func<RowChangingData, Task<(object?, bool)>> ProcessRowF;

            /// <summary>
            /// Create
            /// </summary>
            /// <param name="parentKey"></param>
            /// <param name="tableDef"></param>
            /// <param name="diffRowList"></param>
            /// <param name="processRowF"></param>
            public TraversalData(object? parentKey, TableDef tableDef, List<PersistonDiff.DiffRow> diffRowList, 
                Func<RowChangingData, Task<(object?, bool)>> processRowF)
            {
                ParentKey = parentKey;
                TableDef = tableDef;
                DiffRowList = diffRowList;
                ProcessRowF = processRowF;
            }
        }

        /// <summary>
        /// Used in sevaral data saving methods to identify everything needed for the SQL calls
        /// </summary>
        protected class RowChangingData
        {
            /// <summary>
            /// primary key of parent row (use to set parent key on new rows)
            /// </summary>
            public object? ParentKey;

            /// <summary>
            /// table being saved
            /// </summary>
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
            public Row? PristineRow;
            
            /// <summary>
            /// The modified row (null if DiffRow indicates a deleted row)
            /// </summary>
            public Row? ModifiedRow;

            /// <summary>
            /// Create
            /// </summary>
            /// <param name="parentKey"></param>
            /// <param name="tableDef"></param>
            /// <param name="diffRowList"></param>
            /// <param name="diffRow"></param>
            public RowChangingData(object? parentKey, TableDef tableDef, List<PersistonDiff.DiffRow> diffRowList, PersistonDiff.DiffRow diffRow)
            {
                ParentKey = parentKey;
                TableDef = tableDef;
                DiffRowList = diffRowList;
                DiffRow = diffRow;
            }
        }

        /// <summary>
        /// Save persiston to database
        /// </summary>
        /// <param name="pristineDaton">null or the version before the diff was applied</param>
        /// <param name="modifiedDaton">the validated final version</param>
        /// <param name="diff">the difference between pristine and modified, which is what this method inspects to make the changes</param>
        /// <param name="db"></param>
        /// <param name="user"></param>
        public virtual async Task Save(IDbConnection db, IUser user, Persiston? pristineDaton, Persiston modifiedDaton, PersistonDiff diff)
        {
            if (diff.DatonDef.MainTableDef == null) throw new Exception("Expected main table to be defined in Save");

            //called for each row in traversal; return true to recurse over children
            async Task<(object?, bool)> rowCallback(RowChangingData cdata)
            {
                if (cdata.DiffRow.Kind == DiffKind.DeletedRow)
                {
                    if (cdata.PristineRow == null) throw new Exception("Expected row for deletion in Save.rowCallback");
                    await DeleteRowWithCascade(db, cdata.TableDef, cdata.PristineRow);
                    return (null, false); //don't recur to children of deleted row
                }
                object? newpk = await InsertUpdateRow(db, cdata);
                return (newpk, true);
            }

            var tdata = new TraversalData(null, diff.DatonDef.MainTableDef, diff.MainTable, rowCallback) { 
                PristineList = null,
                ModifiedList = null,
            };
            if (diff.DatonDef.MultipleMainRows)
            {
                var mainListField = diff.DatonDef.Type.GetField(tdata.TableDef.Name);
                tdata.PristineList = mainListField.GetValue(pristineDaton) as IList;
                tdata.ModifiedList = mainListField.GetValue(modifiedDaton) as IList;
                await TraverseDiffList(tdata, null, null);
            } 
            else
            {
                await TraverseDiffList(tdata, pristineDaton, modifiedDaton);
            }
        }

        /// <summary>
        /// Call ProcessRowF on all rows in the diff list, and recurse to all child tables. 
        /// </summary>
        /// <param name="singleMainPristineRow">only set for top level call if there is a single main row</param>
        /// <param name="singleMainModifiedRow">only set for top level call if there is a single main row</param>
        /// <param name="tdata"></param>
        protected async Task TraverseDiffList(TraversalData tdata, Row? singleMainPristineRow, Row? singleMainModifiedRow)
        {
            var pkField = tdata.TableDef.RowType.GetField(tdata.TableDef.PrimaryKeyFieldName);

            foreach (var row in tdata.DiffRowList)
            {
                if (tdata.TableDef.PrimaryKeyFieldName == null 
                    || !row.Columns.TryGetValue(tdata.TableDef.PrimaryKeyFieldName, out object? rowKey)) 
                    throw new Exception($"Diff row is missing primary key {tdata.TableDef.PrimaryKeyFieldName}");

                //find pristine row
                Row? pristineRow = singleMainPristineRow;
                if (tdata.PristineList != null && row.Kind != DiffKind.NewRow)
                {
                    int rowIdx = Utils.IndexOfPrimaryKeyMatch(tdata.PristineList, pkField, rowKey);
                    if (rowIdx >= 0) pristineRow = tdata.PristineList[rowIdx] as Row;
                }

                //find modified row 
                Row? modifiedRow = singleMainModifiedRow;
                if (tdata.ModifiedList != null)
                {
                    int rowIdx = Utils.IndexOfPrimaryKeyMatch(tdata.ModifiedList, pkField, rowKey);
                    if (rowIdx >= 0) modifiedRow = tdata.ModifiedList[rowIdx] as Row;
                }

                //callback to process row
                var cdata = new RowChangingData(tdata.ParentKey, tdata.TableDef, tdata.DiffRowList, row) 
                { 
                    PristineRow = pristineRow,
                    ModifiedRow = modifiedRow
                };
                (object? newPK, bool doTraverseChildren) = await tdata.ProcessRowF(cdata);
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
                        var listField = tdata.TableDef.RowType.GetField(childTableDef.Name);

                        //get pristine list
                        IList? childPristineList = null;
                        if (pristineRow != null)
                            childPristineList = listField.GetValue(pristineRow) as IList;

                        //get modified list
                        IList? childModifiedList = null;
                        if (modifiedRow != null)
                            childModifiedList = listField.GetValue(modifiedRow) as IList;

                        var childTdata = new TraversalData(rowKey, childTableDef, childRows, tdata.ProcessRowF)
                        { 
                            PristineList = childPristineList,
                            ModifiedList = childModifiedList,
                        };
                        await TraverseDiffList(childTdata, null, null);
                    }
                }
            }
        }
    }
}
