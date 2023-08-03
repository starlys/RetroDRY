using System;
using System.Collections;
using System.Collections.Generic;

#pragma warning disable IDE0019

namespace RetroDRY
{
    /// <summary>
    /// Base class for main row and child rows in datons. 
    /// </summary>
    public abstract class Row
    {
        private Dictionary<string, object?>? CustomValues;

        /// <summary>
        /// Set a custom value
        /// </summary>
        public void SetCustom(string name, object? value)
        {
            CustomValues ??= new Dictionary<string, object?>();
            CustomValues[name] = value;
        }

        /// <summary>
        /// Get a custom value or null if not found
        /// </summary>
        public object? GetCustom(string name)
        {
            if (CustomValues == null) return null;
            if (CustomValues.TryGetValue(name, out object? value)) return value;
            return null;
        }

        /// <summary>
        /// Get a standard or custom value from this row
        /// </summary>
        public object? GetValue(ColDef coldef)
        {
            if (coldef.IsCustom) return GetCustom(coldef.FieldName);
            return GetType().GetField(coldef.FieldName).GetValue(this);
        }

        /// <summary>
        /// Set a standard or custom value in this row
        /// </summary>
        /// <param name="value">may be null even for value types (oddly this actually works)</param>
        /// <param name="coldef"></param>
        public void SetValue(ColDef coldef, object? value)
        {
            if (coldef.IsCustom) SetCustom(coldef.FieldName, value);
            else GetType().GetField(coldef.FieldName).SetValue(this, value);
        }

        /// <summary>
        /// When overridden, computes values of computed columns in the row
        /// </summary>
        /// <param name="daton">will be set normally, but null in an export streaming context</param>
        public virtual void Recompute(Daton? daton) { }

        /// <summary>
        /// Clone all fields and child rows declared in tabledef
        /// </summary>
        public object Clone(TableDef tableDef) 
        {
            var target = Utils.Construct(tableDef.RowType) as Row
                ?? throw new Exception("Cannot construct row in Clone");

            //copy custom fields in this row
            if (CustomValues != null) target.CustomValues = new Dictionary<string, object?>(CustomValues);

            //copy other fields in this row
            foreach (var colDef in tableDef.Cols)
            {
                if (colDef.IsCustom) continue;
                var targetField = tableDef.RowType.GetField(colDef.FieldName);
                if (targetField == null) continue;
                var value = targetField.GetValue(this); 
                targetField.SetValue(target, value);
            }

            //recursively copy child rows
            if (tableDef.Children != null)
            {
                foreach (var childTableDef in tableDef.Children)
                {
                    var listField = tableDef.RowType.GetField(childTableDef.Name);
                    var sourceList = listField.GetValue(this) as IList;
                    if (sourceList != null)
                    {
                        var targetList = Utils.CreateOrGetFieldValue<IList>(target, listField)
                            ?? throw new Exception("Uninitialized row list in Clone");
                        foreach (var row in sourceList)
                            if (row is Row trow) targetList.Add(trow.Clone(childTableDef));
                    }
                }
            }

            return target;
        }
    }
}
