using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Text;

namespace RetroDRY
{
    /// <summary>
    /// A string builder for SELECT statements
    /// </summary>
    public class SqlSelectBuilder
    {
        /// <summary>
        /// Where clause of a SQL selement statment
        /// </summary>
        public class Where
        {
            private int LastParamNoUsed = -1;
            private readonly List<object> Parameters = new List<object>();

            /// <summary>
            /// clauses use parameter names in order. For example if the whereclause[0] uses @p1 and @p2 then @p3 is used next
            /// </summary>
            private readonly List<string> Parts = new List<string>();

            /// <summary>
            /// Returns a unique name starting with '@' over the lifetime of thie Where instance
            /// </summary>
            public string NextParameterName() => "@p" + (++LastParamNoUsed);

            /// <summary>
            /// Add a caller-defined where clause
            /// </summary>
            /// <param name="clause"></param>
            public void AddWhere(string clause)
            {
                Parts.Add(clause);
            }

            /// <summary>
            /// When using parameters you must use NextParameterName and embed that name inside clause, in the order that you include parameter values
            /// </summary>
            public void AddWhere(string clause, params object[] _params)
            {
                Parts.Add(clause);
                Parameters.AddRange(_params);
            }

            /// <summary>
            /// get the complete where clause such as "where ... and ... and ...", or empty string if no parts are defined
            /// </summary>
            public override string ToString()
            {
                if (Parts.Any()) return "where " + string.Join(" and ", Parts);
                return "";
            }

            /// <summary>
            /// Add all parameters to a Command 
            /// </summary>
            public void ExportParameters(IDbCommand cmd)
            {
                for (int pno = 0; pno < Parameters.Count; ++pno)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = "p" + pno;
                    p.Value = Parameters[pno];
                    cmd.Parameters.Add(p);
                }
            }
        }

        /// <summary>
        /// Caller may overwrite this or use the default instance
        /// </summary>
        public Where WhereClause = new Where();

        /// <summary>
        /// Column name for order-by clause
        /// </summary>
        public string SortColumnName;

        /// <summary>
        /// zero for unlimited or the page size to load; the query will be set up to actually return one more row than the number here, so
        /// the caller can determine if there is another page or not
        /// </summary>
        public int PageSize;

        /// <summary>
        /// 0-based number to load
        /// </summary>
        public int PageNo;

        private readonly string MainTable;
        private readonly List<string> ReturnColumnNames;
        private readonly SqlFlavorizer SqlFlavor;

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="returnColumnNames">if null or empty, will select all columns</param>
        /// <param name="mainTable">main table name</param>
        /// <param name="sortColName">column to use for order by clause</param>
        /// <param name="sqlFlavor">identifies vendor syntax exceptions</param>
        public SqlSelectBuilder(SqlFlavorizer sqlFlavor, string mainTable, string sortColName, List<string> returnColumnNames)
        {
            SqlFlavor = sqlFlavor;
            MainTable = mainTable;
            SortColumnName = sortColName;
            ReturnColumnNames = returnColumnNames;
            if (ReturnColumnNames == null || ReturnColumnNames.Count == 0) ReturnColumnNames = new[] { "*" }.ToList();
        }

        /// <summary>
        /// Get the SQL command text
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            //validate
            if (SortColumnName == null) throw new Exception("Sort column must be defined");

            string retCols = string.Join(",", ReturnColumnNames);
            string sql = $"select {retCols} from {MainTable} {WhereClause.ToString() ?? ""} order by {MainTable}.{SortColumnName}";
            if (PageSize > 0) sql += SqlFlavor.BuildPagingClause(PageNo, PageSize);
            return sql;
        }

        /// <summary>
        /// Format a list of string or numeric keys into a list of values, such as: 1,'two',3,'O''Malley'
        /// where strings are quoted and escaped.
        /// </summary>
        public static string FormatInClauseList(IEnumerable<object> keys)
        {
            var buf = new StringBuilder(keys.Count() * 4);
            foreach (var x in keys)
            {
                if (buf.Length > 0) buf.Append(',');
                if (x is string s) buf.Append(EscapedQuotedString(s));
                else buf.Append(x.ToString());
            }
            return buf.ToString();
        }

        /// <summary>
        /// Return an escaped and quoted SQL literal of a string. Example: "O'Neal" becomes "'O''Neal'"
        /// </summary>
        public static string EscapedQuotedString(string s) => $"'{s.Replace("'", "''")}'";
    }
}
