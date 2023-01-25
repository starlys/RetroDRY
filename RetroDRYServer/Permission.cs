using System;
using System.Collections.Generic;

namespace RetroDRY
{
    /// <summary>
    /// A collection of permissions granted to a role
    /// </summary>
    public class RetroRole
    {
        /// <summary>
        /// level to use for all tables, unless overriden in TableOverrides
        /// </summary>
        public PermissionLevel BaseLevel;

        /// <summary>
        /// Optional resolver function taking user, daton; if missing, BaseLevel is used.
        /// Daton will be null when checking security for a new unsaved persiston.
        /// </summary>
        public Func<IUser, Daton, PermissionLevel> Level;

        /// <summary>
        /// null or a list of table overrides
        /// </summary>
        public List<TablePermission> TableOverrides;
    }

    /// <summary>
    /// A granted permission for a table. If the table is a main table in a daton, then all child
    /// tables inherit automatically without having to declare another TablePermission for the child table.
    /// </summary>
    public class TablePermission
    {
        /// <summary>
        /// The name matching the field name in the daton class (which may be different than the SQL table name)
        /// </summary>
        public string TableName;

        /// <summary>
        /// level to use for all columns, unless overriden in ColumnOverrides
        /// </summary>
        public PermissionLevel BaseLevel;

        /// <summary>
        /// Optional resolver function taking user, daton, table name; if missing, BaseLevel is used
        /// Daton will be null when checking security for a new unsaved persiston.
        /// </summary>
        public Func<IUser, Daton, string, PermissionLevel> Level;

        /// <summary>
        /// null or a list of column overrides
        /// </summary>
        public List<ColumnPermission> ColumnOverrides;
    }

    /// <summary>
    /// A granted permission override for a single column
    /// </summary>
    public class ColumnPermission
    {
        /// <summary>
        /// Column name
        /// </summary>
        public string ColumnName;

        /// <summary>
        /// Persmission override for the column
        /// </summary>
        public PermissionLevel BaseLevel;

        /// <summary>
        /// Optional resolver function taking user, daton, table name, column name; if missing, BaseLevel is used
        /// Daton will be null when checking security for a new unsaved persiston.
        /// </summary>
        public Func<IUser, Daton, string, string, PermissionLevel> Level;
    }
}
