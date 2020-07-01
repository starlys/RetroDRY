using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RetroDRY
{
    public partial class RetroSql
    {
        protected class LoadColInfo
        {
            public int Index;
            public FieldInfo Field;
            public string SqlExpression;
        }

        protected class LoadResult
        {
            //dict is indexed by parent key for child tables, or has a single index "" for main table
            public Dictionary<object, List<Row>> RowsByParentKey;
            public bool IsComplete;
        }

        /// <summary>
        /// Load daton from database. Caller is responsible for setting the version (this does not deal with locks or versions)
        /// </summary>
        /// <param name="pageSize">only inspected for viewons main table</param>
        /// <returns>null if not found</returns>
        public virtual async Task<Daton> Load(IDbConnection db, DataDictionary dbdef, DatonKey key, int pageSize)
        {
            var datondef = dbdef.FindDef(key);

            //handle viewon paging and ordering
            string sortColName = datondef.MainTableDef.DefaulSortColName;
            int pageNo = 0;
            if (key is ViewonKey vkey)
            {
                pageNo = vkey.PageNumber;
                if (vkey.SortColumnName != null)
                    sortColName = vkey.SortColumnName;
            }
            else pageSize = 0; //prohibit paging in persistons

            //load main table
            var whereClause = MainTableWhereClause(datondef.MainTableDef, key);
            var loadResult = await LoadTable(db, dbdef, datondef.MainTableDef, whereClause, sortColName, pageSize, pageNo);
            loadResult.RowsByParentKey.TryGetValue("", out var rowsForParent);

            //single-main-row datons cannot have zero main rows
            if (!datondef.MultipleMainRows && (rowsForParent == null || rowsForParent.Count == 0)) return null; //was throw new Exception("Single-main-row not found using: " + whereClause.ToString());

            Daton daton = Utils.Construct(datondef.Type) as Daton;
            if (datondef.MultipleMainRows)
            {
                if (daton is Viewon viewon) viewon.IsCompleteLoad = loadResult.IsComplete;

                //copy rowsDict into daton's main table IList
                var listField = datondef.Type.GetField(datondef.MainTableDef.Name);
                if (rowsForParent != null)
                {
                    var list = Utils.CreateOrGetFieldValue<IList>(daton, listField);
                    foreach (var row in rowsForParent) list.Add(row);
                }
            }
            else //single main row
            {
                if (rowsForParent != null)
                    daton = rowsForParent?[0] as Daton;
            }

            //child tables
            var rowsByPK = RestructureByPrimaryKey(datondef.MainTableDef, loadResult.RowsByParentKey);
            await LoadChildTablesRecursive(rowsByPK, db, dbdef, datondef.MainTableDef);

            daton.Key = key;
            daton.Recompute(datondef);
            return daton;
        }

        /// <summary>
        /// Get where clause for main table, or null if none
        /// </summary>
        protected SqlSelectBuilder.Where MainTableWhereClause(TableDef tabledef, DatonKey key)
        {
            if (key is PersistonKey pkey) return MainTableWhereClause(tabledef, pkey);
            if (key is ViewonKey vkey) return MainTableWhereClause(tabledef, vkey);
            return null;
        }

        /// <summary>
        /// Get where clause for persiston main table, or null if this is a whole-table persiston
        /// </summary>
        protected virtual SqlSelectBuilder.Where MainTableWhereClause(TableDef tabledef, PersistonKey key)
        {
            if (key.WholeTable) return null;
            var pkType = tabledef.FindCol(tabledef.PrimaryKeyColName).CSType;
            object pk = key.PrimaryKey; //always string here
            if (pkType != typeof(string))
                pk = Utils.ChangeType(pk, pkType);
            var w = new SqlSelectBuilder.Where();
            w.AddWhere($"{tabledef.PrimaryKeyColName}={w.NextParameterName()}", pk);
            return w;
        }

        /// <summary>
        /// Get where clause for viewon main table
        /// </summary>
        protected virtual SqlSelectBuilder.Where MainTableWhereClause(TableDef tabledef, ViewonKey key)
        {
            var w = new SqlSelectBuilder.Where();
            foreach (var cri in key.Criteria)
            {
                var coldef = tabledef.FindCol(cri.Name);
                if (coldef != null)
                {
                    var crihelper = new ViewonCriterion(coldef, cri.PackedValue);
                    crihelper.ExportWhereClause(w, SqlFlavor);
                }
            }
            return w;
        }

        /// <summary>
        /// Load rows given by where clause for a table and return them as dictionary indexed by the parent key value.
        /// If there is no parent key for the table, then the dictionary will have one entry with key="".
        /// The returned list members are objects of the type indicated by tabledef.
        /// The implementation loads one additional row to determine whether the load was complete, if pageSize is nonzero.
        /// </summary>
        /// <param name="pageSize">if zero, does not do paging</param>
        /// <param name="whereClause">can be null</param>
        protected virtual Task<LoadResult> LoadTable(IDbConnection db, DataDictionary dbdef, TableDef tabledef, SqlSelectBuilder.Where whereClause,
            string sortColName, int pageSize, int pageNo)
        {
            var colInfos = BuildColumnsToLoadList(dbdef, tabledef);
            string parentKeyColName = tabledef.ParentKeyColumnName;
            int parentKeyColIndex = -1;
            var columnNames = colInfos.Select(c => c.SqlExpression).ToList();
            if (parentKeyColName != null)
            {
                parentKeyColIndex = columnNames.Count;
                columnNames.Add(parentKeyColName);
            }
            int customColIndex = -1;
            if (tabledef.HasCustomColumns)
            {
                customColIndex = columnNames.Count;
                columnNames.Add(CUSTOMCOLNAME);
            }
            var sql = new SqlSelectBuilder(SqlFlavor, tabledef.SqlTableName, sortColName, columnNames) {
                PageSize = pageSize,
                PageNo = pageNo
            };
            if (whereClause != null) sql.WhereClause = whereClause;
            var rowsByParentKey = new Dictionary<object, List<Row>>();
            bool isComplete = true;

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = CustomizeSqlStatement(sql.ToString());
                if (whereClause != null)
                    whereClause.ExportParameters(cmd);
                using (var reader = cmd.ExecuteReader())
                {
                    int rowsLoaded = 0;
                    while (reader.Read())
                    {
                        //if this is the throw-away row that was one more than the page size, then note as incomplete but don't look at it
                        if (++rowsLoaded > pageSize && pageSize > 0)
                        {
                            isComplete = false;
                            break;
                        }

                        //read parent key value
                        object pk = "";
                        if (parentKeyColIndex >= 0)
                            pk = reader.GetValue(parentKeyColIndex);

                        //read remaining values
                        var row = Utils.Construct(tabledef.RowType) as Row;
                        SetRowFromReader(colInfos, reader, row);

                        //read custom values
                        if (tabledef.HasCustomColumns) SetRowFromCustomValues(reader.GetString(customColIndex), tabledef, row);

                        //store in return dict
                        if (!rowsByParentKey.ContainsKey(pk)) rowsByParentKey[pk] = new List<Row>();
                        rowsByParentKey[pk].Add(row);
                    }
                }
            }
            var ret = new LoadResult
            {
                RowsByParentKey = rowsByParentKey,
                IsComplete = isComplete
            };
            return Task.FromResult(ret);
        }

        /// <summary>
        /// Called from Load after loading the top table to load all other tables.
        /// The base implementation is careful to only load a table once regardless of the number of detail levels and the number of parent/grandparent
        /// rows. For example for a persiston whose table structure is (Sale, LineItem, LineItemNote), then there will only be one select statement
        /// issued for LineItemNote, using an IN clause for the primary keys of LineItem (which were already loaded).
        /// (The call syntax would be complex to invoke this for the top table so this is called only once for the children of the top
        /// table.)
        /// </summary>
        /// <param name="parentRows">parent Row objects indexed by primary key value</param>
        protected async Task LoadChildTablesRecursive(Dictionary<object, Row> parentRows, IDbConnection db, DataDictionary dbdef, TableDef parentdef)
        {
            if (!parentRows.Any()) return;
            if (parentdef.Children == null) return;

            //get parent keys in the form "1,2,3..."
            string parentKeyListFormatted = SqlSelectBuilder.FormatInClauseList(parentRows.Keys);

            foreach (var childTabledef in parentdef.Children)
            {
                //get rows - where clause is "parentkey in (...)" 
                var whereClause = new SqlSelectBuilder.Where();
                whereClause.AddWhere($"{childTabledef.ParentKeyColumnName} in({parentKeyListFormatted})");
                var loadResult = await LoadTable(db, dbdef, childTabledef, whereClause, childTabledef.DefaulSortColName, 0, 0);
                var rowdict = loadResult.RowsByParentKey;

                //deal out the rows into the parent objects' Lists of this type
                var listField = parentdef.RowType.GetField(childTabledef.Name);
                foreach (object parentKey in rowdict.Keys)
                {
                    var rowsForParent = rowdict[parentKey];
                    var parentRow = parentRows[parentKey];
                    var list = Utils.CreateOrGetFieldValue<IList>(parentRow, listField);
                    foreach (var row in rowsForParent) list.Add(row);
                }

                //recur
                var rowsByPK = RestructureByPrimaryKey(childTabledef, rowdict);
                await LoadChildTablesRecursive(rowsByPK, db, dbdef, childTabledef);
            }
        }

        /// <summary>
        /// Restructure rows from the format returned by LoadTable into simple dictionary of rows indexed by primary key
        /// </summary>
        protected static Dictionary<object, Row> RestructureByPrimaryKey(TableDef tabledef, Dictionary<object, List<Row>> rowdict)
        {
            var pkField = tabledef.RowType.GetField(tabledef.PrimaryKeyColName);
            var flattenedRows = rowdict.Values.SelectMany(x => x);
            var rowsByPK = new Dictionary<object, Row>();
            foreach (var row in flattenedRows)
            {
                object rowpk = pkField.GetValue(row);
                rowsByPK[rowpk] = row;
            }
            return rowsByPK;
        }

        /// <summary>
        /// Create ColInfo list (only non-custom)
        /// </summary>
        protected List<LoadColInfo> BuildColumnsToLoadList(DataDictionary dbdef, TableDef tabledef)
        {
            int colIdx = -1;
            var ret = new List<LoadColInfo>();
            foreach (var coldef in tabledef.Cols)
            {
                if (coldef.IsCustom) continue; 
                ret.Add(new LoadColInfo
                {
                    Index = ++colIdx,
                    SqlExpression = SqlColumnExpression(dbdef, tabledef, coldef),
                    Field = tabledef.RowType.GetField(coldef.Name)
                });
            }
            return ret;
        }

        /// <summary>
        /// Generate the expression to use in the SELECT clause. For regular columns this is just the column name. For left-joined
        /// columns, this is a sub-select statement.
        /// </summary>
        /// <param name="dbdef"></param>
        /// <param name="tabledef"></param>
        /// <param name="coldef"></param>
        /// <returns></returns>
        protected virtual string SqlColumnExpression(DataDictionary dbdef, TableDef tabledef, ColDef coldef)
        {
            //auto-left-joined col
            if (coldef.LeftJoin != null)
            {
                var fkCol = tabledef.FindCol(coldef.LeftJoin.ForeignKeyColumnName);
                if (fkCol == null) throw new Exception($"Invalid foreign key column name in LeftJoin info on {coldef.Name}; it must be the name of a column in the same table");
                if (fkCol.ForeignKeyDatonTypeName == null) throw new Exception($"Invalid use of foreign key column in LeftJoin; {fkCol.Name} must use a ForeignKey annotation to identify the foriegn table");
                var foreignTabledef = dbdef.FindDef(fkCol.ForeignKeyDatonTypeName).MainTableDef;
                string tableAlias = "_t_" + (++MaxtDynamicAliasUsed);
                return $"(select {coldef.LeftJoin.RemoteDisplayColumnName} from {foreignTabledef.SqlTableName} {tableAlias} where {tableAlias}.{foreignTabledef.PrimaryKeyColName}={tabledef.SqlTableName}.{fkCol.Name})";
            }

            if (coldef.IsCustom || coldef.IsComputed) throw new Exception("Cannot load custom or computed column from database");

            //regular col
            return coldef.Name;
        }

        /// <summary>
        /// Set fields in target to values from datareader
        /// </summary>
        protected void SetRowFromReader(IEnumerable<LoadColInfo> colInfos, IDataReader reader, Row target)
        {
            foreach (var ci in colInfos)
            {
                object value = reader.GetValue(ci.Index);
                if (value is DBNull) value = null;
                else value = Utils.ChangeType(value, ci.Field.FieldType);
                ci.Field.SetValue(target, value);
            }
        }

        /// <summary>
        /// unpack the json value and set all custom values in the row
        /// </summary>
        private void SetRowFromCustomValues(string json, TableDef tabledef, Row row)
        {
            var customs = JsonConvert.DeserializeObject<JObject>(json, Constants.CamelSerializerSettings);
            foreach (var coldef in tabledef.Cols.Where(c => c.IsCustom))
            {
                var node = customs[coldef.Name];
                if (node == null) continue;
                object value = Retrovert.ParseNode(node, coldef.CSType);
                row.SetCustom(coldef.Name, value);
            }
        }
    }
}
