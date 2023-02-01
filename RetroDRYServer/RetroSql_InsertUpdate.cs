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
        protected async Task<object?> InsertUpdateRow(IDbConnection db, RowChangingData cdata)
        {
            if (cdata.TableDef.PrimaryKeyFieldName == null || SqlFlavor == null)
                throw new Exception("Must initialize TableDef.PrimaryKeyFieldName and RetroSql.SqlFlavor");
            var primaryKeyField = cdata.TableDef.FindColDefOrThrow(cdata.TableDef.PrimaryKeyFieldName);

            if (cdata.DiffRow.Kind == DiffKind.NewRow)
            {
                if (cdata.ModifiedRow == null)
                    throw new Exception("Expected ModifiedRow when DiffKind is NewRow");

                var builder = new SqlInsertBuilder(SqlFlavor, CustomizeSqlStatement);
                bool dbAssignsKey = cdata.TableDef.DatabaseAssignsKey;
                PopulateWriterColumns(builder, cdata, !dbAssignsKey);
                if (cdata.TableDef.ParentKeySqlColumnName != null)
                    builder.AddNonKey(cdata.TableDef.ParentKeySqlColumnName, null, cdata.ParentKey);
                var newKeyValue = await builder.Execute(db, cdata.TableDef.SqlTableName, primaryKeyField.SqlColumnName, dbAssignsKey);

                //populate the new key value in Modified persiston's row
                if (newKeyValue != null)
                {
                    var rr = new RowRecurPoint(cdata.TableDef, cdata.ModifiedRow);
                    rr.SetPrimaryKey(newKeyValue);
                }
                return newKeyValue;
            }
            if (cdata.DiffRow.Kind == DiffKind.Other)
            {
                var builder = new SqlUpdateBuilder(SqlFlavor, CustomizeSqlStatement);
                PopulateWriterColumns(builder, cdata, false);
                if (!cdata.DiffRow.Columns.TryGetValue(cdata.TableDef.PrimaryKeyFieldName, out object? pkValue))
                    throw new Exception($"Cannot update row in {cdata.TableDef.Name} because no primary key was found in diff");
                if (builder.NonKeyCount > 0)
                    await builder.Execute(db, cdata.TableDef.SqlTableName, primaryKeyField.SqlColumnName, pkValue);
            }
            return null;
        }

        /// <summary>
        /// Add all non-key values from a diff row to a SqlWriteBuilder
        /// </summary>
        protected virtual void PopulateWriterColumns(SqlWriteBuilder builder, RowChangingData cdata, bool includePrimaryKey)
        {
            var customColValues = new Dictionary<string, object?>(); //contents must be json compatible types

            //write to builder; write custom cols to temporary dictionary
            foreach (var coldef in cdata.TableDef.Cols)
            {
                if (coldef.IsComputedOrJoined) continue;
                if (!includePrimaryKey && coldef.FieldName == cdata.TableDef.PrimaryKeyFieldName) continue;
                if (!cdata.DiffRow.Columns.TryGetValue(coldef.FieldName, out object? value)) continue;
                if (coldef.IsCustom)
                    customColValues[coldef.FieldName] = value;
                else
                    builder.AddNonKey(coldef.SqlColumnName, coldef.WireType, value);
            }

            //if any custom columns are to be written, include CustomValues column with old and new values/
            //Example: if custom cols A and B exist, and the current write only updates A, we still need to include the pristine value of B in the json
            if (customColValues.Any())
            {
                if (cdata.PristineRow != null)
                {
                    foreach (var coldef in cdata.TableDef.Cols.Where(c => c.IsCustom))
                    {
                        if (customColValues.ContainsKey(coldef.FieldName)) continue; //don't overwrite old over new
                        customColValues[coldef.FieldName] = cdata.PristineRow.GetCustom(coldef.FieldName);
                    }
                }
                string json = CustomValuesToJson(cdata.TableDef, customColValues);
                builder.AddNonKey(CUSTOMCOLNAME, null, json, useJson: true);
            }
        }

        /// <summary>
        /// Convert a dictionary of custom values indexed by custom column name into json format for database storage
        /// </summary>
        public string CustomValuesToJson(TableDef tabldef, Dictionary<string, object?> values)
        {
            var buf = new StringBuilder(values.Count * 20);
            var wri0 = new StringWriter(buf);
            var writer = new JsonTextWriter(wri0);
            writer.WriteStartObject();
            foreach (var coldef in tabldef.Cols.Where(c => c.IsCustom))
            {
                if (!values.TryGetValue(coldef.FieldName, out object? value)) continue;
                writer.WritePropertyName(coldef.FieldName);
                writer.WriteRawValue(Retrovert.FormatRawJsonValue(coldef, value));
            }
            writer.WriteEndObject();
            return buf.ToString();
        }

    }
}
