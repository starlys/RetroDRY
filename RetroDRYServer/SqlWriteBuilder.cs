using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Base class for SQL INSERT and UPDATE statement building
    /// </summary>
    public abstract class SqlWriteBuilder
    {
        protected class Col
        {
            public string Name, ParameterName;
            public object Value;
            public string LiteralExpression; //for example "@p1"; this gets built into the SQL command text

            public IDbDataParameter AsParameter(IDbCommand cmd)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = ParameterName;
                p.Value = Value ?? DBNull.Value;
                return p;
            }
        }

        protected int LastParamNoUsed = -1;
        protected readonly List<Col> Cols = new List<Col>();
        protected readonly SqlFlavorizer SqlFlavor;
        protected readonly Func<string, string> SqlCustomizer;

        public SqlWriteBuilder(SqlFlavorizer sqlFlavor, Func<string, string> sqlCustomizer)
        {
            SqlFlavor = sqlFlavor;
            SqlCustomizer = sqlCustomizer;
        }

        /// <summary>
        /// Add a column name/value which is not the primary key column
        /// </summary>
        /// <param name="wiretype">can be null; if not set, then nullable logic is bypassed</param>
        public void AddNonKey(string name, string wiretype, object value, bool useJson = false)
        {
            //fix some nullable issues
            if (wiretype == Constants.TYPE_STRING && value == null) value = "";
            if (wiretype == Constants.TYPE_BLOB && value == null) value = new byte[0];

            string paramName = "p" + (++LastParamNoUsed);
            string litExpression = SqlFlavor.LiteralUpdateColumnExperession(name, paramName, useJson);

            Cols.Add(new Col
            {
                Name = name,
                Value = value,
                ParameterName = paramName,
                LiteralExpression = litExpression
            });
        }

        /// <summary>
        /// Change a column value (meant to be used by custom overrides, not used in framework)
        /// </summary>
        public void ChangeValue(string name, object value)
        {
            var col = Cols.FirstOrDefault(c => c.Name == name);
            if (col != null) col.Value = value;
        }

        public int NonKeyCount => Cols.Count;
    }

    public class SqlInsertBuilder : SqlWriteBuilder
    {
        public SqlInsertBuilder(SqlFlavorizer sqlFlavor, Func<string, string> sqlCustomizer) : base(sqlFlavor, sqlCustomizer) { }

        /// <summary>
        /// Execute the insert statement, and return the assigned primary key
        /// </summary>
        /// <param name="databaseAssignsKey">if true, returns newly assigned key</param>
        /// <returns>null or a newly assigned key</returns>
        public Task<object> Execute(IDbConnection db, string tableName, string pkColName, bool databaseAssignsKey)
        {
            string nonKeyColNames = string.Join(",", Cols.Select(c => c.Name));
            string valuesList = string.Join(",", Cols.Select(c => c.LiteralExpression));
            using (var cmd = db.CreateCommand())
            {
                foreach (var c in Cols) cmd.Parameters.Add(c.AsParameter(cmd));
                string coreCommand = $"insert into {tableName} ({nonKeyColNames}) values ({valuesList})";

                if (databaseAssignsKey)
                {
                    cmd.CommandText = coreCommand + SqlFlavor.BuildGetIdentityClause(pkColName);
                    cmd.CommandText = SqlCustomizer(cmd.CommandText);
                    return Task.FromResult(cmd.ExecuteScalar());
                }
                else
                {
                    cmd.CommandText = SqlCustomizer(coreCommand);
                    cmd.ExecuteNonQuery();
                    return Task.FromResult<object>(null);
                }
            }
        }
    }

    public class SqlUpdateBuilder : SqlWriteBuilder
    {
        public SqlUpdateBuilder(SqlFlavorizer sqlFlavor, Func<string, string> sqlCustomizer) : base(sqlFlavor, sqlCustomizer) { }

        /// <summary>
        /// Execute the update statement
        /// </summary>
        public Task Execute(IDbConnection db, string tableName, string pkColName, object pkValue)
        {
            string colClauses = string.Join(",", Cols.Select(c => $"{c.Name}={c.LiteralExpression}"));
            using (var cmd = db.CreateCommand())
            {
                foreach (var c in Cols) cmd.Parameters.Add(c.AsParameter(cmd));
                cmd.CommandText = $"update {tableName} set {colClauses} where {pkColName}=@pk";
                cmd.CommandText = SqlCustomizer(cmd.CommandText);
                Utils.AddParameterWithValue(cmd, "pk", pkValue);
                cmd.ExecuteNonQuery();
            }
            return Task.CompletedTask;
        }
    }
}

