﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroDRY
{
    /// <summary>
    /// methods to check permissions based on RetroRoles
    /// </summary>
    public class SecurityGuard
    {
        private readonly IUser User;
        private readonly DataDictionary Dbdef;

        /// <summary>
        /// Convenience shortener to test PermissionLevel.View
        /// </summary>
        /// <param name="lev"></param>
        public static bool CanView(PermissionLevel lev) => (lev & PermissionLevel.View) != 0;

        /// <summary>
        /// Convenience shortener to test PermissionLevel.Modify
        /// </summary>
        /// <param name="lev"></param>
        public static bool CanUpdate(PermissionLevel lev) => (lev & PermissionLevel.Modify) != 0;

        /// <summary>
        /// Convenience shortener to test PermissionLevel.Create
        /// </summary>
        /// <param name="lev"></param>
        public static bool CanCreate(PermissionLevel lev) => (lev & PermissionLevel.Create) != 0;

        /// <summary>
        /// Convenience shortener to test PermissionLevel.Delete
        /// </summary>
        /// <param name="lev"></param>
        public static bool CanDelete(PermissionLevel lev) => (lev & PermissionLevel.Delete) != 0;

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="dbdef"></param>
        /// <param name="user"></param>
        public SecurityGuard(DataDictionary dbdef, IUser user)
        {
            User = user;
            Dbdef = dbdef;
        }

        /// <summary>
        /// Get the final permission level for a user for a daton, and optionally a table and column. Uses
        /// the permissions base level overridden by the injected resolver function
        /// </summary>
        /// <param name="pristineDaton">null for new persistons, or the pristine version being edited</param>
        /// <param name="tablename">null ok</param>
        /// <param name="colname">null ok</param>
        public PermissionLevel FinalLevel(Daton? pristineDaton, string tablename, string? colname)
        {
            var max = PermissionLevel.None;
            foreach (var role in User.Roles)
            {
                var lev = role.BaseLevel;
                if (role.Level != null) lev = role.Level(User, pristineDaton);
                if (tablename != null && role.TableOverrides != null)
                {
                    var trole = role.TableOverrides.FirstOrDefault(o => o.TableName == tablename);
                    if (trole != null)
                    {
                        lev = trole.BaseLevel;
                        if (trole.Level != null) lev = trole.Level(User, pristineDaton, tablename);
                        if (colname != null && trole.ColumnOverrides != null)
                        {
                            var crole = trole.ColumnOverrides.FirstOrDefault(o => o.ColumnName == colname);
                            if (crole != null)
                            {
                                lev = crole.BaseLevel;
                                if (crole.Level != null) lev = crole.Level(User, pristineDaton, tablename, colname);
                            }
                        }
                    }
                }

                max |= lev;
            }
            return max;
        }

        /// <summary>
        /// Return readable strings describing any disallowed writes for this user
        /// </summary>
        /// <param name="pristineDaton">null for new unsaved persistons, else the pristine version being edited</param>
        /// <param name="datondef">definition of pristineDaton</param>
        /// <param name="diff">the proposed changeset, some of which may be disallowed</param>
        public IEnumerable<string> GetDisallowedWrites(Daton? pristineDaton, DatonDef datondef, PersistonDiff diff)
        {
            if (datondef.MainTableDef == null) throw new Exception("Expected main table to be defined in SecurityGuard");

            var errors = new List<string>();
            FindDisallowedWrites(errors, pristineDaton, datondef.MainTableDef, diff.MainTable);
            return errors;
        }


        /// <summary>
        /// Modify the given daton by setting fields to null if the user does not have permission to view 
        /// </summary>
        public void HidePrivateParts(Daton daton)
        {
            var datondef = Dbdef.FindDef(daton);
            var recur = RecurPoint.FromDaton(datondef, daton);
            if (recur is TableRecurPoint rt)
                HidePrivateParts(daton, rt);
            else if (recur is RowRecurPoint rr)
            {
                //clear out the invisible cols in the single main row 
                var invisibleCols = GetInvisibleCols(daton, rr);
                ClearInvisibleCols(invisibleCols, rr.Row);

                HidePrivateParts(daton, rr);
            }
        }

        private void HidePrivateParts(Daton daton, RowRecurPoint rr)
        {
            foreach (var rt in rr.GetChildren())
                HidePrivateParts(daton, rt);
        }

        private void HidePrivateParts(Daton daton, TableRecurPoint rt) 
        {
            //clear out the invisible cols in each row and recur
            var invisibleCols = GetInvisibleCols(daton, rt);
            foreach (var rr in rt.GetRows())
            {
                ClearInvisibleCols(invisibleCols, rr.Row);
                HidePrivateParts(daton, rr);
            }
        }

        /// <summary>
        /// Locate all writes that are disallowed for the table and append readable errors to the errors list
        /// </summary>
        private void FindDisallowedWrites(List<string> errors, Daton? pristineDaton, TableDef tabledef, List<PersistonDiff.DiffRow> table)
        {
            var unwritableColNames = GetUnwritableColumnNames(pristineDaton, tabledef);
            var tableLev = FinalLevel(pristineDaton, tabledef.Name, null);
            bool canCreate = CanCreate(tableLev), canDelete = CanDelete(tableLev);
            foreach (var row in table)
            {
                if (row.Kind == DiffKind.DeletedRow)
                {
                    if (!canDelete) errors.Add($"Unallowed: delete {tabledef.Name} rows");
                }
                else if (row.Kind == DiffKind.NewRow)
                {
                    if (!canCreate) errors.Add($"Unallowed: create {tabledef.Name} rows");
                }
                else //update
                {
                    foreach (string colName in unwritableColNames)
                    {
                        if (row.Columns.ContainsKey(colName))
                            errors.Add($"Unallowed: update {tabledef.Name}.{colName}");
                    }
                }

                //recur to child tables 
                if (row.ChildTables != null)
                {
                    foreach (var childTableDef in row.ChildTables.Keys)
                        FindDisallowedWrites(errors, pristineDaton, childTableDef, row.ChildTables[childTableDef]);
                }
            }
        }

        /// <summary>
        /// determine which cols are not visible
        /// </summary>
        private List<ColDef> GetInvisibleCols(Daton daton, RecurPoint r)
        {
            var invisibles = new List<ColDef>();
            foreach (var coldef in r.TableDef.Cols)
            {
                var colLev = FinalLevel(daton, r.TableDef.Name, coldef.FieldName);
                if (!CanView(colLev)) invisibles.Add(coldef);
            }
            return invisibles;
        }

        /// <summary>
        /// Set fields in row to null (or default for the type) for each field in invisibleFields
        /// </summary>
        private void ClearInvisibleCols(List<ColDef> invisibleCols, Row row)
        {
            foreach (var invisibleCol in invisibleCols)
                row.SetValue(invisibleCol, null);
        }

        /// <summary>
        /// determine which col names are not writable
        /// </summary>
        private List<string> GetUnwritableColumnNames(Daton? pristineDaton, TableDef tabledef)
        {
            var unwritableCols = new List<string>();
            foreach (var coldef in tabledef.Cols)
            {
                if (coldef.FieldName == tabledef.PrimaryKeyFieldName) continue;
                var colLev = FinalLevel(pristineDaton, tabledef.Name, coldef.FieldName);
                bool canUpdateExisting = CanUpdate(colLev);
                bool canUpdateNew = pristineDaton == null && CanCreate(colLev); //new row doesn't require edit permission, only create
                bool canUpdate = canUpdateExisting || canUpdateNew;
                if (!canUpdate) unwritableCols.Add(coldef.FieldName);
            }
            return unwritableCols;
        }

    }
}
