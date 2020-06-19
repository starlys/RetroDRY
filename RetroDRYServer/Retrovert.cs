using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

#pragma warning disable IDE0019

namespace RetroDRY
{
    /// <summary>
    /// Conversion functions for JSON to/from daton subtypes
    /// </summary>
    public static class Retrovert
    {
        /// <summary>
        /// JSON serializing conversion for preserving raw JSON
        /// </summary>
        public class CondensedDatonResponseConverter : JsonConverter<CondensedDatonResponse>
        {
            public override CondensedDatonResponse ReadJson(JsonReader reader, Type objectType, CondensedDatonResponse existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, CondensedDatonResponse value, JsonSerializer serializer)
            {
                writer.WriteRawValue(value.CondensedDatonJson);
            }
        }

        /// <summary>
        /// Convert daton to JSON wire format
        /// </summary>
        public static string ToWire(DataDictionary dbdef, Daton daton, bool compatibleFormat)
        {
            var datondef = dbdef.FindDef(daton);
            var buf = new StringBuilder(1000);
            var writerOLD = new StringWriter(buf);
            var writer = new JsonTextWriter(writerOLD);
            writer.WriteStartObject();
            writer.WritePropertyName("key");
            writer.WriteValue(daton.Key.ToString());
            writer.WritePropertyName("version");
            writer.WriteValue(daton.Version);
            if (daton is Viewon viewon && !viewon.IsCompleteLoad)
            {
                writer.WritePropertyName("isComplete");
                writer.WriteValue(false);
            }
            var r = RecurPoint.FromDaton(datondef, daton);
            if (compatibleFormat)
            {
                if (r is TableRecurPoint rt) WriteToCompatibleWire(writer, rt);
                if (r is RowRecurPoint rr)
                {
                    writer.WritePropertyName(CamelCasify(datondef.MainTableDef.Name));
                    WriteToCompatibleWire(writer, rr);
                }
            }
            else //dense
            {
                writer.WritePropertyName("content");
                if (r is TableRecurPoint rt) WriteToDenseWire(writer, rt);
                if (r is RowRecurPoint rr)
                {
                    writer.WriteStartArray();
                    WriteToDenseWire(writer, rr);
                    writer.WriteEndArray();
                }
            }
            writer.WriteEndObject();
            return buf.ToString();
        }

        /// <summary>
        /// Given parsed untyped JSON in full compatible format, construct it into a typed daton
        /// </summary>
        public static Daton FromCompatibleWireFull(DataDictionary dbdef, JObject jroot)
        {
            var datonKey = DatonKey.Parse(jroot.Value<string>("key"));
            var datondef = dbdef.FindDef(datonKey);
            var daton = Utils.Construct(datondef.Type) as Daton;
            daton.Key = datonKey;
            daton.Version = jroot.Value<string>("version");
            if (daton is Viewon viewon && jroot.Value<bool>("isComplete") == false)
                viewon.IsCompleteLoad = false;

            var mainRowsNode = jroot[CamelCasify(datondef.MainTableDef.Name)];
            if (mainRowsNode == null) return daton;
            if (!(mainRowsNode is JArray mainRowsArray)) throw new Exception($"{datondef.MainTableDef.Name} node must be an array");
            if (datondef.MultipleMainRows)
            {
                var targetListInfo = datondef.Type.GetField(datondef.MainTableDef.Name);
                if (targetListInfo == null) throw new Exception($"Expected {datondef.MainTableDef.Name} to be a member of {datondef.Type.Name}");
                var targetList = Utils.CreateOrGetFieldValue<IList>(daton, targetListInfo) as IList;
                ReadCompatibleJsonRowArray(mainRowsArray, datondef.MainTableDef, (IList)targetList);
            }
            else
            {
                if (mainRowsArray.Count != 1) throw new Exception($"{datondef.MainTableDef.Name} node must have one element for this daton type");
                ReadCompatibleJsonRow(mainRowsArray[0] as JObject, datondef.MainTableDef, daton);
            }

            daton.Recompute(datondef);
            return daton;
        }

        /// <summary>
        /// Given parsed untyped JSON in diff format, construct a PersistonDiff
        /// </summary>
        public static PersistonDiff FromDiff(DataDictionary dbdef, JObject jroot)
        {
            var datonKey = DatonKey.Parse(jroot.Value<string>("key"));
            var datondef = dbdef.FindDef(datonKey);
            var diff = new PersistonDiff(datondef, datonKey, jroot.Value<string>("version"));

            ReadJsonDiffRowArray(jroot, datondef.MainTableDef, diff.MainTable);

            //existing single-main-row diffs might not include the primary key in the main row, so add it here
            if (!datondef.MultipleMainRows)
            {
                var mainDiffRow = diff.MainTable.First();
                if (!mainDiffRow.Columns.ContainsKey(datondef.MainTableDef.PrimaryKeyColName))
                {
                    var pkColdef = datondef.MainTableDef.FindCol(datondef.MainTableDef.PrimaryKeyColName);
                    var pk = Utils.ChangeType(((PersistonKey)datonKey).PrimaryKey, pkColdef.CSType);
                    mainDiffRow.Columns[datondef.MainTableDef.PrimaryKeyColName] = pk;
                }
            }

            return diff;
        }

        /// <summary>
        /// Convert a node to a value of the given type; handles dates and other things that aren't in the JSON standard
        /// </summary>
        public static object ParseNode(JToken node, Type toType)
        {
            //null
            if (node.Type == JTokenType.Null) return null;

            //exception types
            if (toType == typeof(byte[])) return Convert.FromBase64String(node.ToObject<string>());

            //all other types supported by json library
            return node.ToObject(toType);
        }

        /// <summary>
        /// Format a value for json raw output, or for creating a persiston key as a string
        /// </summary>
        /// <param name="value">any supported value or null</param>
        public static string FormatRawJsonValue(ColDef coldef, object value) 
        {
            //string jsonQuote(string s) => QUOTE + s.ToString().Replace(@"""", @"""""") + QUOTE;
            string jsonQuote(string s) => JsonConvert.ToString(s);

            //null
            if (value == null) return "null";

            //bool
            if (value is bool vbool) return vbool ? "true" : "false";

            //numbers to string with invariant formatting
            if (value is byte vbyte) return vbyte.ToString(CultureInfo.InvariantCulture);
            if (value is Int16 vi16) return vi16.ToString(CultureInfo.InvariantCulture);
            if (value is Int32 vi32) return vi32.ToString(CultureInfo.InvariantCulture);
            if (value is Int64 vi64) return vi64.ToString(CultureInfo.InvariantCulture);
            if (value is double vdouble) return vdouble.ToString(CultureInfo.InvariantCulture);
            if (value is decimal vdec) return vdec.ToString(CultureInfo.InvariantCulture);

            //quoted string
            if (value is string vs) return jsonQuote(vs);

            //date and datetime
            if(value is DateTime vdate)
            {
                if (coldef.WireType == Constants.TYPE_DATE || coldef.WireType == Constants.TYPE_NDATE) 
                    return jsonQuote(vdate.Date.ToString("yyyy-MM-dd"));
                else return jsonQuote(vdate.ToString("O"));
            }

            //byte[]
            if (value is byte[] vba) return jsonQuote(Convert.ToBase64String(vba));

            //unknown
            throw new Exception($"Type {value.GetType().Name} not supported");
        }

        /// <summary>
        /// Look for array nodes matching tableDef, such as Customer, Customer-new, Customer-deleted; and then 
        /// construct target rows based on the values found in those arrays
        /// </summary>
        private static void ReadJsonDiffRowArray(JObject parent, TableDef tableDef, List<PersistonDiff.DiffRow> targetA)
        {
            void ParseArray(string arrayName, DiffKind kind, bool allowChildren, bool fillInMissingNegativeKey) 
            {
                var rows = parent[arrayName];
                if (rows == null) return;
                if(!(rows is JArray rowsA)) throw new Exception($"Diff json nodes must be arrays");
                foreach (var childNode in rowsA)
                {
                    if (!(childNode is JObject childObject)) throw new Exception("Diff array members must be row objects, not values or arrays");
                    var target = new PersistonDiff.DiffRow() { Kind = kind };
                    targetA.Add(target);
                    ReadJsonDiffRow(childObject, target, tableDef, allowChildren);

                    //if the client omits -1 primary key value on new rows, add it here; the save logic needs it but it is redundant from the client perspective
                    if (fillInMissingNegativeKey && !target.Columns.ContainsKey(tableDef.PrimaryKeyColName))
                    {
                        var newRowPK = Utils.ChangeType(-1, tableDef.FindCol(tableDef.PrimaryKeyColName).CSType);
                        target.Columns[tableDef.PrimaryKeyColName] = newRowPK;
                    }
                }
            }

            string name = CamelCasify(tableDef.Name);
            ParseArray(name, DiffKind.Other, true, false);
            ParseArray(name + "-new", DiffKind.NewRow, true, true);
            ParseArray(name + "-deleted", DiffKind.DeletedRow, false, false);
        }

        private static void ReadJsonDiffRow(JObject node, PersistonDiff.DiffRow target, TableDef tableDef, bool allowChildren)
        {
            //copy fields in this row
            foreach (var colDef in tableDef.Cols)
            {
                //get json value or skip
                var jtoken = node.GetValue(colDef.Name, StringComparison.OrdinalIgnoreCase);
                if (jtoken == null) continue;

                //copy
                var value = ParseNode(jtoken, colDef.CSType);
                target.Columns[colDef.Name] = value;
            }

            //recursively copy child rows
            if (tableDef.Children != null && allowChildren)
            {
                foreach (var childTableDef in tableDef.Children)
                {
                    if (target.ChildTables == null) target.ChildTables = new Dictionary<TableDef, List<PersistonDiff.DiffRow>>();
                    if (!target.ChildTables.ContainsKey(childTableDef)) target.ChildTables[childTableDef] = new List<PersistonDiff.DiffRow>();
                    ReadJsonDiffRowArray(node, childTableDef, target.ChildTables[childTableDef]);
                }
            }
        }

        /// <summary>
        /// copy array of rows from node to target (a List of any row type), based on tableDef
        /// </summary>
        private static void ReadCompatibleJsonRowArray(JArray node, TableDef tableDef, IList target)
        {
            foreach (var childNode in node)
            {
                if (!(childNode is JObject childObject)) throw new Exception("Array members must be row objects, not values or arrays");
                var newRow = Utils.Construct(tableDef.RowType) as Row; 
                ReadCompatibleJsonRow(childObject, tableDef, newRow);
                target.Add(newRow);
            }
        }

        /// <summary>
        /// copy row from node (a json object) to target (a daton root or child row), based on tableDef
        /// </summary>
        private static void ReadCompatibleJsonRow(JObject node, TableDef tableDef, Row targetRow)
        {
            //copy fields in this row
            foreach (var colDef in tableDef.Cols)
            {
                //get value from json or skip
                var jtoken = node.GetValue(colDef.Name, StringComparison.OrdinalIgnoreCase);
                if (jtoken == null) continue;
                var value = ParseNode(jtoken, colDef.CSType);

                //write to row
                if (colDef.IsCustom)
                    targetRow.SetCustom(colDef.Name, value);
                else
                {
                    var targetField = tableDef.RowType.GetField(colDef.Name);
                    if (targetField == null) throw new Exception($"Expected {colDef.Name} to be a member of {tableDef.RowType.Name}");
                    targetField.SetValue(targetRow, value);
                }
            }

            //recursively copy child rows
            if (tableDef.Children != null)
            {
                foreach (var childTableDef in tableDef.Children)
                {
                    //get json node or skip
                    var jtoken = node.GetValue(childTableDef.Name, StringComparison.OrdinalIgnoreCase);
                    if (!(jtoken is JArray jarray)) continue;

                    //get target object or skip
                    var targetListField = tableDef.RowType.GetField(childTableDef.Name);
                    if (targetListField == null) continue;
                    var listType = targetListField.FieldType;
                    if (!(targetListField is IList) || !listType.IsGenericType) continue; //is not List<xxx>
                    var list = Utils.CreateOrGetFieldValue<IList>(targetRow, targetListField);

                    //loop rows
                    foreach (var node2 in jarray)
                    {
                        if (!(node2 is JObject node3)) throw new Exception("Array elements must be JSON objects");
                        var row = Utils.Construct(childTableDef.RowType) as Row;
                        list.Add(row);
                        ReadCompatibleJsonRow(node3, childTableDef, row);
                    }
                }
            }
        }

        /// <summary>
        /// Write one row in condensed wire format. Output is [val, val, ...]
        /// </summary>
        private static void WriteToDenseWire(JsonTextWriter writer, RowRecurPoint rr)
        {
            writer.WriteStartArray();
            foreach (var coldef in rr.TableDef.Cols)
                WriteColValue(writer, rr.TableDef.RowType, rr.Row, coldef);
            foreach (var rt in rr.GetChildren())
                WriteToDenseWire(writer, rt);
            writer.WriteEndArray();
        }

        /// <summary>
        /// Write one child table in dense wire format. Output is [row, row...] where row is the output from the other overload
        /// </summary>
        private static void WriteToDenseWire(JsonTextWriter writer, TableRecurPoint rt)
        {
            writer.WriteStartArray();
            foreach (var rr in rt.GetRows())
                WriteToDenseWire(writer, rr);
            writer.WriteEndArray();
        }

        /// <summary>
        /// Write one row in compatible wire format, without property name. Output is: {colname:value, ...}
        /// </summary>
        private static void WriteToCompatibleWire(JsonTextWriter writer, RowRecurPoint rr)
        {
            writer.WriteStartObject();
            foreach (var coldef in rr.TableDef.Cols)
            {
                writer.WritePropertyName(CamelCasify(coldef.Name));
                WriteColValue(writer, rr.TableDef.RowType, rr.Row, coldef);
            }
            foreach (var rt in rr.GetChildren())
                WriteToCompatibleWire(writer, rt);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Write one child table in compatible wire format, with property name. Output is: tablename: [...]
        /// </summary>
        private static void WriteToCompatibleWire(JsonTextWriter writer, TableRecurPoint rt)
        {
            writer.WritePropertyName(CamelCasify(rt.TableDef.Name));
            writer.WriteStartArray();
            foreach (var rr in rt.GetRows())
                WriteToCompatibleWire(writer, rr);
            writer.WriteEndArray();
        }

        /// <summary>
        /// Write a value (quoted string or number); the caller should have written the property name to the writer already
        /// </summary>
        private static void WriteColValue(JsonTextWriter writer, Type rowType, Row row, ColDef coldef)
        {
            object value;
            if (coldef.IsCustom) value = row.GetCustom(coldef.Name); 
            else value = rowType.GetField(coldef.Name).GetValue(row);
            string jsonValue = FormatRawJsonValue(coldef, value);
            writer.WriteRawValue(jsonValue);
        }

        /// <summary>
        /// Convert the serializable portions of a user's permission set to a wire-ready structure. Injected functions are ignored here.
        /// </summary>
        /// <returns>null if none defined</returns>
        public static PermissionResponse PermissionsToWire(IUser user)
        {
            if (user.Roles == null) return null;

            var wholeW = new PermissionResponse();
            var tableWDict = new Dictionary<string, DetailPermisionResponse>(); //table level permissions indexed by table name
            foreach (var role in user.Roles)
            {
                wholeW.Level |= (int)role.BaseLevel;
                if (role.TableOverrides != null) {
                    foreach (var table in role.TableOverrides)
                    {
                        if (!tableWDict.TryGetValue(table.TableName, out var tableW))
                        {
                            tableW = new DetailPermisionResponse() { Name = table.TableName };
                            tableWDict[table.TableName] = tableW;
                        }
                        tableW.Level |= (int)table.BaseLevel;
                        if (table.ColumnOverrides != null)
                        {
                            if (tableW.Overrides == null) tableW.Overrides = new List<DetailPermisionResponse>();
                            foreach (var col in table.ColumnOverrides)
                            {
                                var existing = tableW.Overrides.FirstOrDefault(x => x.Name == col.ColumnName);
                                if (existing == null)
                                    tableW.Overrides.Add(new DetailPermisionResponse { Name = col.ColumnName, Level = (int)col.BaseLevel });
                                else
                                    existing.Level |= (int)col.BaseLevel;
                            }
                        }
                    }
                }
            }
            wholeW.Overrides = tableWDict.Values.ToList();
            return wholeW;
        }

        /// <summary>
        /// Convert the serializable portions of a data dictionary to a wire-ready structure.
        /// </summary>
        public static DataDictionaryResponse DataDictionaryToWire(DataDictionary ddict, IUser user)
        {
            var datonWires = new List<DatonDefResponse>();
            foreach (var name in ddict.DatonDefs.Keys)
            {
                var datondef = ddict.DatonDefs[name];
                datonWires.Add(new DatonDefResponse
                {
                    Name = name,
                    IsPersiston = typeof(Persiston).IsAssignableFrom(datondef.Type),
                    CriteriaDef = ToWire(datondef.CriteriaDef, user, true),
                    MainTableDef = ToWire(datondef.MainTableDef, user, false),
                    MultipleMainRows = datondef.MultipleMainRows
                });
            }
            return new DataDictionaryResponse
            {
                DatonDefs = datonWires
            };
        }

        //see DataDictionaryToWire
        private static TableDefResponse ToWire(TableDef source, IUser user, bool isCriteria)
        {
            if (source == null) return null;
            var wire = new TableDefResponse()
            {
                Name = CamelCasify(source.Name),
                Cols = source.Cols.Select(c => ToWire(c, user)).ToList(),
                PrimaryKeyColName = CamelCasify(source.PrimaryKeyColName),
                Prompt = DataDictionary.ResolvePrompt(source.Prompt, user, source.Name),
                IsCriteria = isCriteria,
                Children = source.Children?.Select(t => ToWire(t, user, false)).ToList()
            };
            return wire;
        }

        //see DataDictionaryToWire
        private static ColDefResponse ToWire(ColDef c, IUser user)
        {
            return new ColDefResponse
            {
                AllowSort = c.AllowSort,
                ForeignKeyDatonTypeName = c.ForeignKeyDatonTypeName,
                LookupViewonTypeName = c.LookupViewonTypeName,
                LookupViewonKeyColumnName = CamelCasify(c.LookupViewonKeyColumnName),
                LeftJoinForeignKeyColumnName = CamelCasify(c.LeftJoin?.ForeignKeyColumnName),
                LeftJoinRemoteDisplayColumnName = CamelCasify(c.LeftJoin?.RemoteDisplayColumnName),
                ImageUrlColumName = c.Image?.UrlColumName,
                IsComputed = c.IsComputed,
                IsMainColumn = c.IsMainColumn,
                IsVisibleInDropdown = c.IsVisibleInDropdown,
                LengthValidationMessage = DataDictionary.ResolvePrompt(c.LengthValidationMessage, user),
                MaxLength = c.MaxLength,
                MaxNumberValue = c.MaxNumberValue,
                MinLength = c.MinLength,
                MinNumberValue = c.MinNumberValue,
                Name = CamelCasify(c.Name),
                Prompt = DataDictionary.ResolvePrompt(c.Prompt, user, c.Name),
                RangeValidationMessage = DataDictionary.ResolvePrompt(c.RangeValidationMessage, user),
                Regex = c.Regex,
                RegexValidationMessage = DataDictionary.ResolvePrompt(c.RegexValidationMessage, user),
                WireType = c.WireType
            };
        }

        /// <summary>
        /// return s with the first letter converted to lowercase
        /// </summary>
        public static string CamelCasify(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (char.IsUpper(s, 0)) s = char.ToLowerInvariant(s[0]) + s.Substring(1);
            return s;
        }
    }
}
