using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Short term cache of display values to use for generating readable output of datons in server contexts.
    /// NOT thread safe.
    /// </summary>
    public class LookupResolver
    {
        readonly Func<DatonKey, Task<(DatonDef?, Daton?)>> DatonGetter;

        /// <summary>
        /// Dictionary of display values indexed by key, within a dictionary indexed by daton.
        /// For example: Values[datonKey][1] = 'Sold' where datonKey is the key of a whole-table persiston for status codes.
        /// </summary>
        readonly Dictionary<DatonKey, Dictionary<object, string>> Datons = new Dictionary<DatonKey, Dictionary<object, string>>();

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="datonGetter">function to get a referenced daton, or return null on any error</param>
        public LookupResolver(Func<DatonKey, Task<(DatonDef?, Daton?)>> datonGetter)
        {
            DatonGetter = datonGetter;
        }

        /// <summary>
        /// Get the display value fo the given key value in the given daton
        /// </summary>
        /// <param name="datonKey"></param>
        /// <param name="rowKey"></param>
        /// <returns></returns>
        public async Task<string> DisplayValueFor(DatonKey datonKey, object? rowKey)
        {
#pragma warning disable IDE0019 // Use pattern matching
            if (rowKey == null) return "";

            //get the inner dictionary, or create it if not found
            if (!Datons.TryGetValue(datonKey, out Dictionary<object, string> values))
            {
                values = new Dictionary<object, string>();
                Datons[datonKey] = values;
                (var datonDef, var daton) = await DatonGetter(datonKey);
                if (daton != null && datonDef?.MainTableDef != null)
                {
                    var pkColDef = datonDef.MainTableDef.FindColDefOrNull(datonDef.MainTableDef.PrimaryKeyFieldName);
                    var displayColDef = datonDef.MainTableDef.Cols.FirstOrDefault(c => c.IsMainColumn);
                    var recurT = RecurPoint.FromDaton(datonDef, daton) as TableRecurPoint;
                    if (recurT != null && displayColDef != null && pkColDef != null)
                    {
                        var recurRows = recurT.GetRows();
                        if (recurRows.Any())
                        {
                            var rowType = recurRows.First().Row.GetType();
                            var pkField = rowType.GetField(pkColDef.FieldName);
                            var displayField = rowType.GetField(displayColDef.FieldName);
                            if (displayField != null)
                            {
                                foreach (var recurR in recurRows)
                                {
                                    object? pk = pkField.GetValue(recurR.Row);
                                    string s = displayField.GetValue(recurR.Row)?.ToString() ?? "";
                                    if (pk != null) values[pk] = s;
                                }
                            }
                        }
                    }
                }
            }
#pragma warning restore IDE0019 // Use pattern matching

            //get the display value from the inner dictionary
            if (!values.TryGetValue(rowKey, out string display)) return "";
            return display;
        }
    }
}
