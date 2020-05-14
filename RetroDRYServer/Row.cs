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
        private Dictionary<string, object> CustomValues;

        /// <summary>
        /// Set a custom value
        /// </summary>
        public void SetCustom(string name, object value)
        {
            if (CustomValues == null) CustomValues = new Dictionary<string, object>();
            CustomValues[name] = value;
        }

        /// <summary>
        /// Get a custom value or null if not found
        /// </summary>
        public object GetCustom(string name)
        {
            if (CustomValues == null) return null;
            if (CustomValues.TryGetValue(name, out object value)) return value;
            return null;
        }

        /// <summary>
        /// When overridden, computes values of computed columns in the row
        /// </summary>
        public virtual void Recompute(Daton daton) { }

        /// <summary>
        /// Clone all fields and child rows declared in tabledef
        /// </summary>
        public object Clone(TableDef tableDef) 
        {
            var target = Utils.Construct(tableDef.RowType);

            //copy fields in this row
            foreach (var colDef in tableDef.Cols)
            {
                var targetField = tableDef.RowType.GetField(colDef.Name);
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
                        var targetList = Utils.CreateOrGetFieldValue<IList>(target, listField);
                        foreach (var row in sourceList)
                            if (row is Row trow) targetList.Add(trow.Clone(childTableDef));
                    }
                }
            }

            return target;
        }
    }
}
