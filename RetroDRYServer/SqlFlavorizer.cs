﻿using System;

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
    }
}