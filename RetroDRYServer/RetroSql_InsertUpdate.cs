using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RetroDRY
{
    public partial class RetroSql
    {
        /// <summary>
        /// Insert or update a row
        /// </summary>
        /// <returns>database assigned parent key (only if changed) else null</returns>
        protected async Task<object> InsertUpdateRow(IDbConnection db, RowChangingData cdata)
        {
            if (cdata.DiffRow.Kind == DiffKind.NewRow)
            {
                var builder = new SqlInsertBuilder(SqlFlavor, CustomizeSqlStatement);
                bool dbAssignsKey = DatabaseAssignsKeysForTable(cdata.TableDef.Name);
                PopulateWriterColumns(builder, cdata, !dbAssignsKey);
                var newKeyValue = await builder.Execute(db, cdata.TableDef.SqlTableName, cdata.TableDef.PrimaryKeyColName, dbAssignsKey);

                //populate the new key value in Modified persiston's row
                if (newKeyValue != null && cdata.ModifiedRow != null)
                {
                    var rr = new RowRecurPoint() { TableDef = cdata.TableDef, Row = cdata.PristineRow };
                    rr.SetPrimaryKey(newKeyValue);
                }
                return newKeyValue;
            }
            if (cdata.DiffRow.Kind == DiffKind.Other)
            {
                var builder = new SqlUpdateBuilder(SqlFlavor, CustomizeSqlStatement);
                PopulateWriterColumns(builder, cdata, false);
                if (!cdata.DiffRow.Columns.TryGetValue(cdata.TableDef.PrimaryKeyColName, out object pkValue))
                    throw new Exception($"Cannot update row in {cdata.TableDef.Name} because no primary key was found in diff");
                if (builder.NonKeyCount > 0)
                    await builder.Execute(db, cdata.TableDef.SqlTableName, cdata.TableDef.ParentKeyColumnName, pkValue);
            }
            return null;
        }

        /// <summary>
        /// Add all non-key values from a diff row to a SqlWriteBuilder
        /// </summary>
        protected virtual void PopulateWriterColumns(SqlWriteBuilder builder, RowChangingData cdata, bool includePrimaryKey)
        {
            var customColValues = new Dictionary<string, object>(); //contents must be json compatible types

            //write to builder; write custom cols to temporary dictionary
            foreach (var coldef in cdata.TableDef.Cols)
            {
                if (coldef.IsComputed) continue;
                if (!includePrimaryKey && coldef.Name == cdata.TableDef.PrimaryKeyColName) continue;
                if (!cdata.DiffRow.Columns.TryGetValue(coldef.Name, out object value)) continue;
                if (coldef.IsCustom)
                    customColValues[coldef.Name] = Retrovert.FormatRawJsonValue(coldef, value);
                else
                    builder.AddNonKey(coldef.Name, coldef.WireType, value);
            }

            //if any custom columns are to be written, include CustomValues column with old and new values
            if (customColValues.Any())
            {
                if (cdata.PristineRow != null)
                {
                    foreach (var coldef in cdata.TableDef.Cols.Where(c => c.IsCustom))
                    {
                        if (customColValues.ContainsKey(coldef.Name)) continue; //don't overwrite old over new
                        object value = cdata.TableDef.RowType.GetField(coldef.Name).GetValue(cdata.PristineRow);
                        customColValues[coldef.Name] = value;
                    }
                }
                string json = CustomValuesToJson(cdata.TableDef, customColValues);
                builder.AddNonKey(CUSTOMCOLNAME, null, json);
            }
        }

        /// <summary>
        /// Convert a dictionary of custom values indexed by custom column name into json format for database storage
        /// </summary>
        public string CustomValuesToJson(TableDef tabldef, Dictionary<string, object> values)
        {
            var buf = new StringBuilder(values.Count * 20);
            var wri0 = new StringWriter(buf);
            var writer = new JsonTextWriter(wri0);
            writer.WriteStartObject();
            foreach (var coldef in tabldef.Cols.Where(c => c.IsCustom))
            {
                if (!values.TryGetValue(coldef.Name, out object value)) continue;
                writer.WritePropertyName(coldef.Name);
                writer.WriteRawValue(Retrovert.FormatRawJsonValue(coldef, value));
            }
            writer.WriteEndObject();
            return buf.ToString();
        }

    }
}
