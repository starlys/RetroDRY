using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RetroDRY
{
    /// <summary>
    /// Defintion of one table within a DatonDef
    /// </summary>
    public class TableDef
    {
        /// <summary>
        /// For top level single-main-row datons, Name matches the daton class name;
        /// for other cases, Name matches the List member name
        /// </summary>
        public string Name;
        
        /// <summary>
        /// Identifies class containing the row's field declarations
        /// </summary>
        public Type RowType;

        /// <summary>
        /// Columns in table that map to fields in a Row object
        /// </summary>
        public List<ColDef> Cols = new List<ColDef>();

        /// <summary>
        /// Child tables or null
        /// </summary>
        public List<TableDef>? Children; 

        /// <summary>
        /// The field name of the primary key in this table
        /// </summary>
        public string? PrimaryKeyFieldName;

        /// <summary>
        /// True if the database assigns the primary key when the row is inserted; false if the client has to supply the value
        /// </summary>
        public bool DatabaseAssignsKey;

        /// <summary>
        /// The field name of default ordering in this table (also see ColDef.AllowSort)
        /// </summary>
        public string? DefaulSortFieldName;

        /// <summary>
        /// Column name of the parent key in this table, or null if this is a main table.
        /// </summary>
        public string? ParentKeySqlColumnName;

        /// <summary>
        /// The SQL table name (which is often the same as Name)
        /// </summary>
        public string SqlTableName;

        /// <summary>
        /// The optional overridden FROM clause, for specifying inner joins. When specified for a viewon,
        /// this overrides SqlTableName. Not allowed for persistons.
        /// </summary>
        public string? SqlFromClause;

        /// <summary>
        /// Table prompt in natural language indexed by language code (index is "" for default language);
        /// or null to fall back to table name as the prompt
        /// </summary>
        public SortedList<string, string>? Prompt;

        /// <summary>
        /// True when the table contains a column called CustomValues, which stores any number of name-value pairs
        /// </summary>
        public bool HasCustomColumns { get; private set; }

        /// <summary>
        /// Create, setting the name and sql table name to the same
        /// </summary>
        /// <param name="name"></param>
        /// <param name="rowType"></param>
        public TableDef(string name, Type rowType)
        {
            Name = SqlTableName = name;
            RowType = rowType;
        }

        /// <summary>
        /// Convenience method to get the column by name; returns null if not found
        /// </summary>
        public ColDef? FindColDefOrNull(string? name, bool caseSensitive = true)
        {
            if (name == null) return null;
            if (caseSensitive) return Cols.FirstOrDefault(c => c.FieldName == name);
            return Cols.FirstOrDefault(c => string.Compare(c.FieldName, name, true, CultureInfo.InvariantCulture) == 0);
        }

        /// <summary>
        /// Convenience method to get the column by name; throws exception if not found
        /// </summary>
        public ColDef FindColDefOrThrow(string? name, bool caseSensitive = true)
        {
            var cd = FindColDefOrNull(name, caseSensitive);
            if (cd == null) throw new Exception($"Cannot find field {name} in {Name}");
            return cd;
        }

        /// <summary>
        /// Convenience method to get the child table by name or null 
        /// </summary>
        public TableDef? FindChildTable(string name) => Children?.FirstOrDefault(c => c.Name == name);

        /// <summary>
        /// Add a custom column to the table
        /// </summary>
        public ColDef AddCustomColum(string name, Type cstype, string wireType)
        {
            var coldef = new ColDef(name, wireType, cstype)
            {
                IsCustom = true
            };
            Cols.Add(coldef);
            HasCustomColumns = true;
            return coldef;
        }

        /// <summary>
        /// Throw exception if definition is not supported
        /// </summary>
        public void Validate(bool isCriteria)
        {
            foreach (var coldef in Cols) coldef.Validate(isCriteria);
            if (Children != null)
            {
                if (isCriteria) throw new Exception("Criteria tables cannot define child tables");
                foreach (var childTabledef in Children)
                    childTabledef.Validate(false);
            }
        }
    }
}
