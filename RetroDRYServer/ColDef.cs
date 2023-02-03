using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RetroDRY
{
    /// <summary>
    /// A defintion of one table column
    /// </summary>
    public class ColDef
    {
        /// <summary>
        /// Information about how images are handled
        /// </summary>
        public class ImageInfo
        {
            /// <summary>
            /// The FieldName in the same row whose value will be set to the URL of the image
            /// </summary>
            public string? UrlColumName;
        }

        /// <summary>
        /// Information about left-join for one column; for example, how to look up the name associated with the value of a foreign key
        /// </summary>
        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class LeftJoinInfo
        {
            /// <summary>
            /// The foreign key column in this same table
            /// </summary>
            public string? ForeignKeyFieldName;

            /// <summary>
            /// The column in the joined table to join in (usually a name or description column)
            /// </summary>
            public string? RemoteDisplaySqlColumnName;
        }

        /// <summary>
        /// Behavior when editing the column (used when the column references another type)
        /// </summary>
        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class SelectBehaviorInfo
        {
            /// <summary>
            /// The viewon type name used to choose values for this column
            /// </summary>
            public string? ViewonTypeName;

            /// <summary>
            /// If set, the viewon will be seeded with a value for this criterion
            /// </summary>
            public string? AutoCriterionFieldName { get; set; }

            /// <summary>
            /// When used with AutoCriterionFieldName, the viewon's criteria value is taken from the value of this field in the local row
            /// </summary>
            public string? AutoCriterionValueFieldName { get; set; }

            /// <summary>
            /// The value in the viewon's main result table to be copied back into the column being edited; if omitted, the Key column
            /// of the viewon will be used.
            /// </summary>
            public string? ViewonValueFieldName { get; set; }

            /// <summary>
            /// If true, all possible values from the viewon are shown as a dropdown list (only use if you know the number of options will be reasonable);
            /// if false, then the user will open the viewon separately and perform a search
            /// </summary>
            public bool UseDropdown { get; set; }
        }

        /// <summary>
        /// Name of Field in the containing Row class
        /// </summary>
        public string FieldName;

        /// <summary>
        /// If provided, the table prefix to use in sql loads; if missing, uses no prefix (default for single-table select statements)
        /// </summary>
        public string? SqlTableName;

        /// <summary>
        /// SQL column name (normally set the same as FieldName)
        /// </summary>
        public string SqlColumnName;

        /// <summary>
        /// One of the official wire type names (bool, int16, nint16, string, etc) for cross platform use; see Constants
        /// </summary>
        public string WireType;

        /// <summary>
        /// Type in C#, which must match the declaration in the Row class. 
        /// </summary>
        public Type CSType;

        /// <summary>
        /// If false, the column is declared in the containing type; if true the column is declared only at runtime and uses the custom storage in the Row class
        /// </summary>
        public bool IsCustom;

        /// <summary>
        /// If true, the column is declared with the computed attribute, and won't be loaded and saved by default SQL, and won't be editable on clients
        /// </summary>
        public bool IsComputed;

        /// <summary>
        /// If true, this column can be used as ORDER BY clause in loading
        /// </summary>
        public bool AllowSort;

        /// <summary>
        /// The daton type that this column references
        /// </summary>
        public string? ForeignKeyDatonTypeName;

        /// <summary>
        /// Information needed to define selection behavior
        /// </summary>
        public SelectBehaviorInfo? SelectBehavior;

        /// <summary>
        /// Information needed to load this column via a left-join.
        /// </summary>
        public LeftJoinInfo? LeftJoin;

        /// <summary>
        /// If true, this column is the readable name or description that users would see to identify the row (or in some cases it could also be the primary key)
        /// </summary>
        public bool IsMainColumn;

        /// <summary>
        /// If true, this column will be shown in dropdowns along with the one marked with IsMainColumn, when selecting rows
        /// </summary>
        public bool IsVisibleInDropdown;

        /// <summary>
        /// Column prompt in natural language indexed by language code (index is "" for default language);
        /// or this may be null if the Name should be used.
        /// </summary>
        public SortedList<string, string>? Prompt;

        /// <summary>
        /// String minimum length
        /// </summary>
        public int MinLength;

        /// <summary>
        /// String maximum length (or 0 for unlimited)
        /// </summary>
        public int MaxLength;

        /// <summary>
        /// Validation message with optional placeholders: {0}=prompt {1}=maxlength {2}=minlength
        /// (For use of the SortedList, see Prompt property)
        /// </summary>
        public SortedList<string, string>? LengthValidationMessage; 

        /// <summary>
        /// Regular expression for validating values for this column
        /// </summary>
        public string? Regex;

        /// <summary>
        /// Validation message with optional placeholders: {0}=prompt
        /// (For use of the SortedList, see Prompt property)
        /// </summary>
        public SortedList<string, string>? RegexValidationMessage;

        /// <summary>
        /// The minimum numeric value; if MinNumberValue and MaxNumberValue are both 0, then range is not enforced
        /// </summary>
        public decimal MinNumberValue;

        /// <summary>
        /// Seee MinNumberValue
        /// </summary>
        public decimal MaxNumberValue;

        /// <summary>
        /// Validation message with optional placeholders: {0}=prompt {1}=min {2}=max
        /// (For use of the SortedList, see Prompt property)
        /// </summary>
        public SortedList<string, string>? RangeValidationMessage;

        /// <summary>
        /// Set only if this column is an image identifier (such as a file name)
        /// </summary>
        public ImageInfo? Image;

        /// <summary>
        /// If true, the column is computed or left-joined, and won't be loaded and saved by default SQL, and won't be editable on clients
        /// </summary>
        public bool IsComputedOrJoined => IsComputed || LeftJoin != null;

        /// <summary>
        /// Create, setting the sql column name and field name to the same
        /// </summary>
        /// <param name="name"></param>
        /// <param name="wireType"></param>
        /// <param name="cSType"></param>
        public ColDef(string name, string wireType, Type cSType)
        {
            SqlColumnName = FieldName = name;
            WireType = wireType;
            CSType = cSType;
        }

        /// <summary>
        /// qualified table.col name for SQL load expressions
        /// </summary>
        public string SqlExpression => SqlTableName == null ? SqlColumnName : $"{SqlTableName}.{SqlColumnName}";

        /// <summary>
        /// Convenience method to ensure Prompt is non null while setting a prompt for the given language.
        /// Use langCode="" for default language.
        /// </summary>
        public void SetPrompt(string langCode, string prompt)
        {
            Prompt ??= new SortedList<string, string>();
            Prompt[langCode] = prompt;
        }

        /// <summary>
        /// Throw exception if definition is not supported
        /// </summary>
        public void Validate(bool isCriteria)
        {
            bool foundPair = false;
            foreach (var pair in Constants.TypeMap)
                if (pair.Item1 == CSType && pair.Item2 == WireType)
                {
                    foundPair = true;
                    break;
                }
            if (!foundPair) throw new Exception($"Type wire type '{WireType}' cannot be used with the C# type '{CSType.Name}'");

            //criteria can't use nullables
            if (isCriteria && Nullable.GetUnderlyingType(CSType) != null)
                throw new Exception("Cannot use nullable value types in criteria tables: " + FieldName);
        }
    }
}
