using System;
using System.Data;
using System.Threading.Tasks;

namespace RetroDRY
{
    public partial class RetroSql
    {
        /// <summary>
        /// Delete a row and cascade to all its child rows
        /// </summary>
        protected virtual async Task DeleteRowWithCascade(IDbConnection db, TableDef tabledef, Row row)
        {
            //recur to delete child rows
            var p0 = new RowRecurPoint(tabledef, row);
            foreach (var p1 in p0.GetChildren())
            {
                foreach (var p2 in p1.GetRows())
                    await DeleteRowWithCascade(db, p2.TableDef, p2.Row);
            }

            //delete this row
            if (tabledef.SqlTableName == null || tabledef.PrimaryKeyFieldName == null) throw new Exception("Missing table name or key column anme in DeleteRowWithCascade");
            var keyCol = tabledef.FindColDefOrThrow(tabledef.PrimaryKeyFieldName);
            await DeleteSingleRow(db, tabledef.SqlTableName, keyCol.SqlColumnName, p0.GetPrimaryKey());
        }

        /// <summary>
        /// Issue SQL to delete one row from database; no recursion
        /// </summary>
        protected virtual Task DeleteSingleRow(IDbConnection db, string sqlTableName, string keySqlColumnName, object key)
        {
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = $"delete from {sqlTableName} where {keySqlColumnName}=@pk";
                cmd.CommandText = CustomizeSqlStatement(cmd.CommandText);
                Utils.AddParameterWithValue(cmd, "pk", key);
                cmd.ExecuteNonQuery();
            }
            return Task.CompletedTask;
        }
    }
}
