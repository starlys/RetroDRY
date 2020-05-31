using System;
using System.Collections;
using System.Collections.Generic;

namespace RetroDRY
{
    /// <summary>
    /// Collection of values changed, and added/deleted rows of a persiston. Types of the field values must match
    /// the applicable TableDef of the persiston type, but type checking is not done here.
    /// </summary>
    public class PersistonDiff
    {
        public enum ApplyAction { NoChanges, Changes, PersistonDeleted }

        /// <summary>
        /// Storage for changes to a single row
        /// </summary>
        public class DiffRow
        {
            public DiffKind Kind;
            public Dictionary<string, object> Columns = new Dictionary<string, object>();
            public Dictionary<TableDef, List<DiffRow>> ChildTables; //may be null
        }

        public DatonKey Key { get; protected set; }
        public DatonDef DatonDef { get; protected set; }

        /// <summary>
        /// The persiston version from which this diff was built; it will be a new version once the diff is saved
        /// </summary>
        public string BasedOnVersion { get; protected set; }

        /// <summary>
        /// Changes on the main table rows, including nested changes in child rows
        /// </summary>
        public List<DiffRow> MainTable = new List<DiffRow>();

        public PersistonDiff(DatonDef datondef, DatonKey key, string version)
        {
            DatonDef = datondef;
            Key = key;
            BasedOnVersion = version;
        }

        /// <summary>
        /// Apply the changes in this diff to the given target
        /// </summary>
        public ApplyAction ApplyTo(DatonDef datondef, Persiston target) 
        {
            if (MainTable.Count == 0) return ApplyAction.NoChanges;

            if (datondef.MultipleMainRows)
            {
                bool anyChanges = false;
                var field = target.GetType().GetField(datondef.MainTableDef.Name);
                var targetList = Utils.CreateOrGetFieldValue<IList>(target, field);
                if (targetList == null) throw new Exception($"Row class {target.GetType().Name} must include field member {datondef.MainTableDef.Name}");
                foreach (var source in MainTable)
                    anyChanges |= ApplyDiffRowToList(datondef.MainTableDef, source, targetList);
                return anyChanges ? ApplyAction.Changes : ApplyAction.NoChanges;
            }
            else
            {
                //handle top level edge cases to ensure the key matches the new/modified/delete status of the top row
                if (MainTable.Count != 1)
                    throw new Exception("For single-main-table persistons the diff may only include the single main row");
                bool diffIsNewRow = MainTable[0].Kind == DiffKind.NewRow;
                if (diffIsNewRow != target.Key.IsNew)
                    throw new Exception("The key specifies a new row but the diff does not indicate a new row; or the key specifies modified/delete but the diff indicates a new row");
                var source = MainTable[0];
                if (source.Kind == DiffKind.DeletedRow) 
                    return ApplyAction.PersistonDeleted;

                //reached here, so its a plain update of the single row; primary key ignored
                bool anyChanges = false;
                foreach (string colName in source.Columns.Keys)
                    anyChanges |= SetValue(source, colName, target, target.GetType());

                //child tables
                anyChanges |= ApplyChildTables(source, target, target.GetType());

                return anyChanges ? ApplyAction.Changes : ApplyAction.NoChanges;
            }
        }

        /// <summary>
        /// Given a single row of changes (add/new/delete) and a list, either delete or update the list row with the matching primary key,
        /// or add a new row.
        /// </summary>
        /// <returns>true if any actual changes</returns>
        private bool ApplyDiffRowToList(TableDef tabledef, DiffRow source, IList targetList)
        {
            bool anyChanges = false;
            if (tabledef.PrimaryKeyColName == null) throw new Exception("Primary key not defined");
            var itemType = targetList.GetType().GenericTypeArguments[0];
            var pkField = itemType.GetField(tabledef.PrimaryKeyColName);
            if (pkField == null) throw new Exception($"Primary key field not found in type {itemType.Name}");

            if (source.Kind == DiffKind.DeletedRow)
            {
                if (!source.Columns.TryGetValue(tabledef.PrimaryKeyColName, out object pkToDelete)) throw new Exception("Deleted row in diff needs primary key member");
                int idxToDelete = Utils.IndexOfPrimaryKeyMatch(targetList, pkField, pkToDelete);
                if (idxToDelete >= 0)
                {
                    targetList.RemoveAt(idxToDelete);
                    anyChanges = true;
                }
            }

            else //new or update
            {
                //find or create row
                Row target;
                if (source.Kind == DiffKind.NewRow)
                {
                    target = Utils.Construct(itemType) as Row;
                    targetList.Add(target);
                    anyChanges = true;
                }
                else
                {
                    if (!source.Columns.TryGetValue(tabledef.PrimaryKeyColName, out object pkToUpdate)) throw new Exception("Updated row in diff needs primary key member");
                    int idxToUpdate = Utils.IndexOfPrimaryKeyMatch(targetList, pkField, pkToUpdate);
                    if (idxToUpdate < 0) throw new Exception("Row to update is not found");
                    target = targetList[idxToUpdate] as Row;
                }

                //copy values
                foreach (string colName in source.Columns.Keys)
                    anyChanges |= SetValue(source, colName, target, itemType);

                //process child tables
                anyChanges |= ApplyChildTables(source, target, itemType);
            }
            return anyChanges;
        }

        /// <summary>
        /// Set value of colName in target from source
        /// </summary>
        /// <returns>true if it was a real change; false if same value</returns>
        private bool SetValue(DiffRow source, string colName, Row target, Type targetType)
        {
            var field = targetType.GetField(colName);
            if (field != null)
            {
                var oldValue = field.GetValue(target);
                var newValue = source.Columns[colName];
                bool isChange = (
                    (oldValue != null && !oldValue.Equals(newValue))
                    ||
                    (!(oldValue == null && newValue == null))
                    );
                if (isChange)
                {
                    field.SetValue(target, newValue);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Process any child table rows from source
        /// </summary>
        /// <returns>true if any real changes made</returns>
        private bool ApplyChildTables(DiffRow source, Row target, Type targetType)
        {
            bool anyChanges = false;
            if (source.ChildTables != null)
            {
                foreach (var child in source.ChildTables)
                {
                    var childTableDef = child.Key;
                    var childRows = child.Value;
                    var field = targetType.GetField(childTableDef.Name);
                    if (field == null) throw new Exception($"Row class {targetType.Name} must include field member {childTableDef.Name}");
                    var childTargetList = Utils.CreateOrGetFieldValue<IList>(target, field);
                    foreach (var childRow in childRows)
                        anyChanges |= ApplyDiffRowToList(childTableDef, childRow, childTargetList);
                }
            }
            return anyChanges;
        }
    }
}
