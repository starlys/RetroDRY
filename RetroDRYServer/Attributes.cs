using System;

namespace RetroDRY
{
    /// <summary>
    /// Hides the class or field from autogeneration of data dictionary
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class RetroHideAttribute : Attribute
    {
    }

    /// <summary>
    /// If applied, changes the SQL table name of the row class; if missing, uses the name of the List member
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SqlTableNameAttribute : Attribute
    {
         public string Name { get; set; }
        public SqlTableNameAttribute(string name) { Name = name; }
    }

    /// <summary>
    /// If applied, the daton has a single main row
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SingleMainRowAttribute : Attribute
    {
    }

    /// <summary>
    /// If applied, it can override the default choice for wire type
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class WireTypeAttribute : Attribute
    {
        public string TypeName { get; set; }
        public WireTypeAttribute(string name) { TypeName = name; }
    }

    /// <summary>
    /// Specifies the column is the primary human-readable description or other identifier for the row
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class MainColumnAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies the column (along with the one marked MainColum) should be shown in dropdowns
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class VisibleInDropdownAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies the column is computed and not user editable
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ComputedColumnAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies which database the table is stored in
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DatabaseNumberAttribute : Attribute
    {
        public int Num { get; set; }
        public DatabaseNumberAttribute(int num) { Num = num; }
    }

    /// <summary>
    /// Specifies the default language prompt for the table or column
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class PromptAttribute : Attribute
    {
        public string Prompt { get; set; }
        public PromptAttribute(string prompt) { Prompt = prompt; }
    }

    /// <summary>
    /// Specifies that this column is the primary key of the table
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class PrimaryKeyAttribute : Attribute
    {
        /// <summary>
        /// True if the database assigns the primary key when the row is inserted; false if the client has to supply the value
        /// </summary>
        public bool DatabaseAssigned { get; set; }
        public PrimaryKeyAttribute(bool databaseAssigned) { DatabaseAssigned = databaseAssigned; }
    }

    /// <summary>
    /// Specifies the column in a child table that contains the reference to the primary key of its parent table
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ParentKeyAttribute : Attribute
    {
        public string ColumnName { get; set; }
        public ParentKeyAttribute(string colname) { ColumnName = colname; }
    }

    /// <summary>
    /// Specifies the persiston whose primary key is referenced by this column
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ForeignKeyAttribute : Attribute
    {
        public Type Target { get; set; }
        public ForeignKeyAttribute(Type target) { Target = target; }
    }

    /// <summary>
    /// Specifies behavior for constrained selection of the value for this column
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class SelectBehaviorAttribute : Attribute
    {
        /// <summary>
        /// The viewon used to select values for this column
        /// </summary>
        public Type ViewonType { get; set; }

        /// <summary>
        /// If set, the viewon will be seeded with a value for this criterion
        /// </summary>
        public string AutoCriterionName { get; set; }

        /// <summary>
        /// When used with AutoCriterionName, the viewon's criteria value is taken from the value of this column in the local row
        /// </summary>
        public string AutoCriterionValueColumnName { get; set; }

        /// <summary>
        /// The value in the viewon's main result table to be copied back into the column being edited; if omitted, the Key column
        /// of the viewon will be used.
        /// </summary>
        public string ViewonValueColumnName { get; set; }

        /// <summary>
        /// If true, all possible values from the viewon are shown as a dropdown list (only use if you know the number of options will be reasonable);
        /// if false, then the user will open the viewon separately and perform a search
        /// </summary>
        public bool UseDropdown { get; set; }

        public SelectBehaviorAttribute(Type target) { ViewonType = target; }
    }

    /// <summary>
    /// Specifies that this column contains an image identifier
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ImageColumnAttribute : Attribute
    {
        /// <summary>
        /// The column name in this same row (usually a calculated column) whose value is a complete URL for the image
        /// </summary>
        public string UrlColumnName { get; set; }
        public ImageColumnAttribute(string colname) { UrlColumnName = colname; }
    }

    /// <summary>
    /// Specifies attribute inheritance
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class InheritFromAttribute : Attribute
    {
        /// <summary>
        /// Identifies the daton and optionally table and column names to copy into this object. Format is "datonname" or "datonname.tablename" or "datonname.tablename.colname"
        /// </summary>
        public string SourceName { get; set; }

        /// <summary>
        /// If true, custom columns are copied from a source persiston into a viewon
        /// </summary>
        public bool IncludeCustom { get; set; }

        public InheritFromAttribute(string source) { SourceName = source; }
    }

    /// <summary>
    /// Specifies that this row class is not a real row, and will not be instantiated, but is instead used to define criteria for the viewon
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CriteriaAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies that the table may be sorted by this column
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class SortColumnAttribute : Attribute
    {
        public bool IsDefault { get; set; }
        public SortColumnAttribute(bool isdefault = true) { IsDefault = isdefault; }
    }

    /// <summary>
    /// Specifies that the column is not actually stored in the table, but is instead loaded with a left-join or sub-query, and is therefore not user editable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class LeftJoinAttribute : Attribute
    {
        /// <summary>
        /// Specifies the name of a column in this same row that references some foreign table (the ForeignKey attribute must be set on that column)
        /// </summary>
        public string ForeignKeyColumnName { get; set; }

        /// <summary>
        /// Specifies a column in the foreign table whose value should be loaded into this column
        /// </summary>
        public string DisplayColumnName { get; set; }

        public LeftJoinAttribute(string fkColName, string displayColName)
        {
            ForeignKeyColumnName = fkColName;
            DisplayColumnName = displayColName;
        }
    }
}
