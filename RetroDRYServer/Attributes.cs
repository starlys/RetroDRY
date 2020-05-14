using System;

namespace RetroDRY
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class RetroHideAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SqlTableNameAttribute : Attribute
    {
         public string Name { get; set; }
        public SqlTableNameAttribute(string name) { Name = name; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SingleMainRowAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class WireTypeAttribute : Attribute
    {
        public string TypeName { get; set; }
        public WireTypeAttribute(string name) { TypeName = name; }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class MainColumnAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class VisibleInDropdownAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ComputedColumnAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DatabaseNumberAttribute : Attribute
    {
        public int Num { get; set; }
        public DatabaseNumberAttribute(int num) { Num = num; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class PromptAttribute : Attribute
    {
        public string Prompt { get; set; }
        public PromptAttribute(string prompt) { Prompt = prompt; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ParentKeyAttribute : Attribute
    {
        public string ColumnName { get; set; }
        public ParentKeyAttribute(string colname) { ColumnName = colname; }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ForeignKeyAttribute : Attribute
    {
        public Type Target { get; set; }
        public ForeignKeyAttribute(Type target) { Target = target; }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ImageColumnAttribute : Attribute
    {
        public string UrlColumnName { get; set; }
        public ImageColumnAttribute(string colname) { UrlColumnName = colname; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class InheritFromAttribute : Attribute
    {
        public string SourceName { get; set; }
        public bool IncludeCustom { get; set; }
        public InheritFromAttribute(string source, bool includeCustom = false) { SourceName = source; IncludeCustom = includeCustom; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CriteriaAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class SortColumnAttribute : Attribute
    {
        public bool IsDefault { get; set; }
        public SortColumnAttribute(bool isdefault = true) { IsDefault = isdefault; }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class LeftJoinAttribute : Attribute
    {
        public string ForeignKeyColumnName { get; set; }
        public string DisplayColumnName { get; set; }
        public LeftJoinAttribute(string fkColName, string displayColName)
        {
            ForeignKeyColumnName = fkColName;
            DisplayColumnName = displayColName;
        }
    }
}
