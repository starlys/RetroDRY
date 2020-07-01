using System;

namespace RetroDRY
{
    /// <summary>
    /// Utility to handle SQL syntax for different server vendors
    /// </summary>
    public class SqlFlavorizer
    {
        public enum VendorKind { PostgreSQL, MySQL, SQLServer }

        public VendorKind Vendor { get; private set; }

        public SqlFlavorizer(VendorKind v)
        {
            Vendor = v;
        }

        /// <summary>
        /// Get the sql string to append to a SELECT..WHERE..ORDER BY string, for this vendor.
        /// Return value has a leading space.
        /// It will return one more row than the given pageSize.
        /// </summary>
        public string BuildPagingClause(int pageNo, int pageSize)
        {
            if (Vendor == VendorKind.SQLServer)
                return $" offset {pageSize * pageNo} rows fetch next {pageSize + 1} rows only"; 
            return $" limit {pageSize + 1} offset {pageSize * pageNo}"; //mysql, postgres
        }

        /// <summary>
        /// Build the clause that can be tacked onto an INSERT statement, which when executed as a scalar query, will 
        /// return the newly assigned primary key. The clause may start with ";" if the vendor requires a separate statement
        /// for selecting the return value.
        /// </summary>
        public string BuildGetIdentityClause(string pkColName)
        {
            if (Vendor == VendorKind.SQLServer)
                return "; select SCOPE_IDENTITY()";
            if (Vendor == VendorKind.MySQL)
                return "; select LAST_INSERT_ID()";
            if (Vendor == VendorKind.PostgreSQL)
                return $" returning {pkColName}";
            return "";
        }

        /// <summary>
        /// Get the LIKE operator for case-insensitive search
        /// </summary>
        public string LikeOperator()
        {
            if (Vendor == VendorKind.PostgreSQL) return "ilike";
            return "like";
        }

        /// <summary>
        /// Convert a user-entered value into a parameter value to be used with the LIKE operator (appends %)
        /// </summary>
        public string LikeParamValue(string s)
        {
            //For SQL Server, another way if we need to is to convert "abc" to "[Aa][Bb][Cc]" which is more performant than "lower(colname) like value"
            return s + "%";
        }

#pragma warning disable IDE0060
        /// <summary>
        /// Format the expression for a SQL column value such that it can fit in the blanks in the following:
        /// "update X set col1=___" or "insert into X (col1) values (___)".
        /// Default behavior is to use the parameter name only such as "@p1"
        /// </summary>
        public string LiteralUpdateColumnExperession(string colName, string paramName, bool useJson)
        {
            if (useJson)
            {
                if (Vendor == VendorKind.PostgreSQL) return $"cast(@{paramName} as jsonb)";
            }
            return "@" + paramName;
        }
    }
#pragma warning restore IDE0060
}
