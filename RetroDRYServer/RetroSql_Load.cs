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
        /// <summary>
        /// Information on how to load a single column
        /// </summary>
        protected class LoadColInfo
        {
            /// <summary>
            /// index in list of select-clause columns
            /// </summary>
            public int Index;

            /// <summary>
            /// reflection info used to set the field value in the daton row
            /// </summary>
            public FieldInfo Field;

            /// <summary>
            /// the string to use in the select clause
            /// </summary>
            public string SqlExpression;

            /// <summary>
            /// Create
            /// </summary>
            /// <param name="index"></param>
            /// <param name="field"></param>
            /// <param name="sqlExpression"></param>
            public LoadColInfo(int index, FieldInfo field, string sqlExpression)
            {
                Index = index;
                Field = field;
                SqlExpression = sqlExpression;
            }
        }

        /// <summary>
        /// intermediate result container for each table
        /// </summary>
        protected class SingleLoadResult
        {
            /// <summary>
            /// rows loaded; dict is indexed by parent key for child tables, or has a single index "" for main table
            /// </summary>
            public Dictionary<object, List<Row>>? RowsByParentKey;

            /// <summary>
            /// true if viewon was loaded to completion; false if there may be more rows
            /// </summary>
            public bool IsComplete;
        }

        /// <summary>
        /// Result container for entire daton load
        /// </summary>
        public class LoadResult
        {
            /// <summary>
            /// Daton being set
            /// </summary>
            public Daton? Daton;

            /// <summary>
            /// user-readable errors
            /// </summary>
            public string[]? Errors;
        }

        /// <summary>
        /// Load daton from database. Caller is responsible for setting the version (this does not deal with locks or versions)
        /// </summary>
        /// <param name="pageSize">only inspected for viewons main table</param>
        /// <param name="db"></param>
        /// <param name="key">identifies daton to load</param>
        /// <param name="dbdef"></param>
        /// <param name="user"></param>
        /// <returns>null if not found</returns>
        public virtual async Task<LoadResult?> Load(IDbConnection db, DataDictionary dbdef, IUser? user, DatonKey key, int pageSize)
        {
            var datondef = dbdef.FindDef(key);
            if (datondef.MainTableDef == null) throw new Exception("Expected main table to be defined in Load");

            //viewon validation
            if (key is ViewonKey vkey2) 
            {
                var validator = new Validator(user);
                await validator.ValidateCriteria(datondef, vkey2);
                if (validator.Errors.Any()) return new LoadResult { Errors = validator.Errors.ToArray() };
            }

            //handle viewon paging and ordering
            string? sortFieldName = datondef.MainTableDef.DefaulSortFieldName;
            int pageNo = 0;
            if (key is ViewonKey vkey)
            {
                pageNo = vkey.PageNumber;
                if (vkey.SortFieldName != null)
                    sortFieldName = vkey.SortFieldName;
            }
            else pageSize = 0; //prohibit paging in persistons

            //load main table
            var whereClause = MainTableWhereClause(datondef.MainTableDef, key);
            var sortField = datondef.MainTableDef.FindColDefOrThrow(sortFieldName);
            var loadResult = await LoadTable(db, dbdef, datondef.MainTableDef, whereClause, sortField, pageSize, pageNo);
            if (loadResult.RowsByParentKey == null) throw new Exception("Expected LoadTable to yield rows");
            loadResult.RowsByParentKey.TryGetValue("", out var rowsForParent);

            //single-main-row datons cannot have zero main rows
            if (!datondef.MultipleMainRows && (rowsForParent == null || rowsForParent.Count == 0)) return null; //was throw new Exception("Single-main-row not found using: " + whereClause.ToString());

            Daton daton = Utils.Construct(datondef.Type) as Daton ?? throw new Exception("Cannot construct daton in Load");
            if (datondef.MultipleMainRows)
            {
                if (daton is Viewon viewon) viewon.IsCompleteLoad = loadResult.IsComplete;

                //copy rowsDict into daton's main table IList
                var listField = datondef.Type.GetField(datondef.MainTableDef.Name);
                if (rowsForParent != null)
                {
                    var list = Utils.CreateOrGetFieldValue<IList>(daton, listField);
                    if (list == null) throw new Exception("Could not get list field value in Load");
                    foreach (var row in rowsForParent) list.Add(row);
                }
            }
            else //single main row
            {
                if (rowsForParent != null)
                    daton = rowsForParent?[0] as Daton ?? throw new Exception("Cannot construct single main row daton in Load");
            }

            //child tables
            var rowsByPK = RestructureByPrimaryKey(datondef.MainTableDef, loadResult.RowsByParentKey);
            await LoadChildTablesRecursive(rowsByPK, db, dbdef, datondef.MainTableDef);

            daton.Key = key;
            daton.Recompute(datondef);
            daton.RecomputeAll(datondef);
            return new LoadResult { Daton = daton };
        }

        /// <summary>
        /// Get where clause for main table, or null if none
        /// </summary>
        protected SqlSelectBuilder.Where? MainTableWhereClause(TableDef tabledef, DatonKey key)
        {
            if (key is PersistonKey pkey) return MainTableWhereClause(tabledef, pkey);
            if (key is ViewonKey vkey) return MainTableWhereClause(tabledef, vkey);
            return null;
        }

        /// <summary>
        /// Get where clause for persiston main table, or null if this is a whole-table persiston
        /// </summary>
        protected virtual SqlSelectBuilder.Where? MainTableWhereClause(TableDef tabledef, PersistonKey key)
        {
            if (key.WholeTable) return null;
            var pkType = tabledef.FindColDefOrThrow(tabledef.PrimaryKeyFieldName).CSType;
            object? pk = key.PrimaryKey; //always string here
            if (pkType != typeof(string))
                pk = Utils.ChangeType(pk, pkType);
            if (pk == null) throw new Exception("Could not change type of key in MainTableWhereClause");
            var w = new SqlSelectBuilder.Where();
            CustomizeWhereClause(w, $"{tabledef.PrimaryKeyFieldName}={w.NextParameterName()}", pk);
            return w;
        }

        /// <summary>
        /// Get where clause for viewon main table
        /// </summary>
        protected virtual SqlSelectBuilder.Where MainTableWhereClause(TableDef tabledef, ViewonKey key)
        {
            if (SqlFlavor == null)
                throw new Exception("Must initialize RetroSql.SqlFlavor");

            var w = new SqlSelectBuilder.Where();
            if (key.Criteria != null)
                foreach (var cri in key.Criteria)
                {
                    var coldef = tabledef.FindColDefOrNull(cri.Name);
                    if (coldef != null)
                    {
                        var crihelper = new ViewonCriterion(coldef, cri.PackedValue);
                        crihelper.ExportWhereClause(this, w, SqlFlavor);
                    }
                }
            return w;
        }

        /// <summary>
        /// Base implementation adds the clause with parameters to the given Where instance. Overrides could modify the string
        /// or params and then call the base version.
        /// </summary>
        /// <param name="where"></param>
        /// <param name="clause"></param>
        /// <param name="_params"></param>
        public virtual void CustomizeWhereClause(SqlSelectBuilder.Where where, string clause, params object[] _params)
        {
            where.AddWhere(clause, _params);
        }

        /// <summary>
        /// Load rows given by where clause for a table and return them as dictionary indexed by the parent key value.
        /// If there is no parent key for the table, then the dictionary will have one entry with key="".
        /// The returned list members are objects of the type indicated by tabledef.
        /// The implementation loads one additional row to determine whether the load was complete, if pageSize is nonzero.
        /// </summary>
        /// <param name="pageSize">if zero, does not do paging</param>
        /// <param name="pageNo">page number to load, 0 for first page</param>
        /// <param name="whereClause">can be null</param>
        /// <param name="dbdef"></param>
        /// <param name="db"></param>
        /// <param name="sortCol"></param>
        /// <param name="tabledef"></param>
        protected virtual Task<SingleLoadResult> LoadTable(IDbConnection db, DataDictionary dbdef, TableDef tabledef, SqlSelectBuilder.Where? whereClause,
            ColDef sortCol, int pageSize, int pageNo)
        {
            if (SqlFlavor == null)
                throw new Exception("Must initialize RetroSql.SqlFlavor");

            var colInfos = BuildColumnsToLoadList(dbdef, tabledef);
            string? parentKeySqlColumnName = tabledef.ParentKeySqlColumnName;
            int parentKeyColIndex = -1;
            var columnNames = colInfos.Select(c => c.SqlExpression).ToList();
            if (parentKeySqlColumnName != null)
            {
                parentKeyColIndex = columnNames.Count;
                columnNames.Add(parentKeySqlColumnName);
            }
            int customColIndex = -1;
            if (tabledef.HasCustomColumns)
            {
                customColIndex = columnNames.Count;
                columnNames.Add(CUSTOMCOLNAME);
            }
            string fromClause = tabledef.SqlFromClause ?? tabledef.SqlTableName;
            var sql = new SqlSelectBuilder(SqlFlavor, fromClause, sortCol.SqlExpression, columnNames) {
                PageSize = pageSize,
                PageNo = pageNo
            };
            if (whereClause != null) sql.WhereClause = whereClause;
            var rowsByParentKey = new Dictionary<object, List<Row>>();
            bool isComplete = true;

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = CustomizeSqlStatement(sql.ToString());
                whereClause?.ExportParameters(cmd);
                using var reader = cmd.ExecuteReader();
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
                    var row = Utils.Construct(tabledef.RowType) as Row ?? throw new Exception("Cannot construct row in LoadTable"); ;
                    SetRowFromReader(colInfos, reader, row);

                    //read custom values
                    if (tabledef.HasCustomColumns)
                    {
                        string? json = (string?)Utils.ChangeType(reader.GetValue(customColIndex), typeof(string));
                        if (!string.IsNullOrEmpty(json))
                            SetRowFromCustomValues(json, tabledef, row);
                    }

                    //store in return dict
                    if (!rowsByParentKey.ContainsKey(pk)) rowsByParentKey[pk] = new List<Row>();
                    rowsByParentKey[pk].Add(row);
                }
            }
            var ret = new SingleLoadResult
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
        /// <param name="db"></param>
        /// <param name="dbdef"></param>
        /// <param name="parentdef">definition of parent table, which has been loaded already</param>
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
                var sortField = childTabledef.FindColDefOrThrow(childTabledef.DefaulSortFieldName);
                CustomizeWhereClause(whereClause, $"{childTabledef.ParentKeySqlColumnName} in({parentKeyListFormatted})");
                var loadResult = await LoadTable(db, dbdef, childTabledef, whereClause, sortField, 0, 0);
                var rowdict = loadResult.RowsByParentKey;
                if (rowdict == null) throw new Exception("Expected RowsByParentKey in LoadChildTablesRecursive");

                //deal out the rows into the parent objects' Lists of this type
                var listField = parentdef.RowType.GetField(childTabledef.Name);
                foreach (object parentKey in rowdict.Keys)
                {
                    var rowsForParent = rowdict[parentKey];
                    var parentRow = parentRows[parentKey];
                    var list = Utils.CreateOrGetFieldValue<IList>(parentRow, listField);
                    if (list == null) throw new Exception("Could not create list field in LoadChildTablesRecursive");
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
            var pkField = tabledef.RowType.GetField(tabledef.PrimaryKeyFieldName);
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
                if (coldef.IsCustom || coldef.IsComputed) continue;
                ret.Add(new LoadColInfo(++colIdx, tabledef.RowType.GetField(coldef.FieldName), SqlColumnExpression(dbdef, tabledef, coldef)));
            }
            return ret;
        }

        /// <summary>
        /// Generate the expression to use in the SELECT clause. For regular columns this is just the column name. 
        /// The table name may be added if SqlTableName is defined in the ColDef.
        /// For left-joined columns, this is a sub-select statement.
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
                var fkCol = tabledef.FindColDefOrNull(coldef.LeftJoin.ForeignKeyFieldName);
                if (fkCol == null) throw new Exception($"Invalid foreign key column name in LeftJoin info on {coldef.FieldName}; it must be the name of a column in the same table");
                if (fkCol.ForeignKeyDatonTypeName == null) throw new Exception($"Invalid use of foreign key column in LeftJoin; {fkCol.FieldName} must use a ForeignKey annotation to identify the foriegn table");
                var foreignTabledef = dbdef.FindDef(fkCol.ForeignKeyDatonTypeName).MainTableDef;
                if (foreignTabledef == null) throw new Exception("Expected main table to be defined in SqlColumExpression");
                var foreignKey = foreignTabledef.FindColDefOrThrow(foreignTabledef.PrimaryKeyFieldName);
                string tableAlias = "_t_" + (++MaxtDynamicAliasUsed);
                return $"(select {coldef.LeftJoin.RemoteDisplaySqlColumnName} from {foreignTabledef.SqlTableName} {tableAlias} where {tableAlias}.{foreignKey.SqlColumnName}={tabledef.SqlTableName}.{fkCol.SqlColumnName})";
            }

            if (coldef.IsCustom || coldef.IsComputedOrJoined) throw new Exception("Cannot load custom or computed column from database");

            //regular col
            return coldef.SqlExpression;
        }

        /// <summary>
        /// Set fields in target to values from datareader
        /// </summary>
        protected void SetRowFromReader(IEnumerable<LoadColInfo> colInfos, IDataReader reader, Row target)
        {
            foreach (var ci in colInfos)
            {
                object? value = reader.GetValue(ci.Index);
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
            if (customs == null) throw new Exception("Expected to deserialize json in SetRowFromCustomValues");
            foreach (var coldef in tabledef.Cols.Where(c => c.IsCustom))
            {
                var node = customs[coldef.FieldName];
                if (node == null) continue;
                object? value = Retrovert.ParseNode(node, coldef.CSType);
                row.SetCustom(coldef.FieldName, value);
            }
        }
    }
}
