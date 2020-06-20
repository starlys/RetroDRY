using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RetroDRY
{
    /// <summary>
    /// methods to check permissions based on RetroRoles
    /// </summary>
    public class SecurityGuard
    {
        private readonly IUser User;
        private readonly DataDictionary Dbdef;

        public static bool CanView(PermissionLevel lev) => (lev & PermissionLevel.View) != 0;
        public static bool CanUpdate(PermissionLevel lev) => (lev & PermissionLevel.Modify) != 0;
        public static bool CanCreate(PermissionLevel lev) => (lev & PermissionLevel.Create) != 0;
        public static bool CanDelete(PermissionLevel lev) => (lev & PermissionLevel.Delete) != 0;

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
        public PermissionLevel FinalLevel(Daton pristineDaton, string tablename, string colname)
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

                //force modify for columns that inhert create permission from table level
                if (colname !=  null && lev == PermissionLevel.Create) lev = PermissionLevel.Create | PermissionLevel.Modify;

                max |= lev;
            }
            return max;
        }

        /// <summary>
        /// Return readable strings describing any disallowed writes for this user
        /// </summary>
        /// <param name="pristineDaton">null for new unsaved persistons, else the pristine version being edited</param>
        public IEnumerable<string> GetDisallowedWrites(Daton pristineDaton, DatonDef datondef, PersistonDiff diff)
        {
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
                var invisibleFields = GetInvisibleFields(daton, rr);
                ClearInvisibleFields(invisibleFields, rr.Row);

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
            var invisibleFields = GetInvisibleFields(daton, rt);
            foreach (var rr in rt.GetRows())
            {
                ClearInvisibleFields(invisibleFields, rr.Row);
                HidePrivateParts(daton, rr);
            }
        }

        /// <summary>
        /// Locate all writes that are disallowed for the table and append readable errors to the errors list
        /// </summary>
        private void FindDisallowedWrites(List<string> errors, Daton pristineDaton, TableDef tabledef, List<PersistonDiff.DiffRow> table)
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
        private List<FieldInfo> GetInvisibleFields(Daton daton, RecurPoint r)
        {
            var invisibleFields = new List<FieldInfo>();
            foreach (var coldef in r.TableDef.Cols)
            {
                var colLev = FinalLevel(daton, r.TableDef.Name, coldef.Name);
                if (!CanView(colLev)) invisibleFields.Add(r.TableDef.RowType.GetField(coldef.Name));
            }
            return invisibleFields;
        }

        /// <summary>
        /// Set fields in row to null (or default for the type) for each field in invisibleFields
        /// </summary>
        private void ClearInvisibleFields(List<FieldInfo> invisibleFields, Row row)
        {
            foreach (var invisibleField in invisibleFields)
                invisibleField.SetValue(row, null); //oddly this works on value types
        }

        /// <summary>
        /// determine which col names are not writable
        /// </summary>
        private List<string> GetUnwritableColumnNames(Daton pristineDaton, TableDef tabledef)
        {
            var unwritableCols = new List<string>();
            foreach (var coldef in tabledef.Cols)
            {
                if (coldef.Name == tabledef.PrimaryKeyColName) continue;
                var colLev = FinalLevel(pristineDaton, tabledef.Name, coldef.Name);
                if (!CanUpdate(colLev)) unwritableCols.Add(coldef.Name);
            }
            return unwritableCols;
        }

    }
}
