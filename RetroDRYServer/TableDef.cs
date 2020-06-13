using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RetroDRY
{
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
        /// Child tables 
        /// </summary>
        public List<TableDef> Children; //may be null

        /// <summary>
        /// The column name of the primary key in this table
        /// </summary>
        public string PrimaryKeyColName;

        /// <summary>
        /// The column name of default ordering in this table (also see ColDef.AllowSort)
        /// </summary>
        public string DefaulSortColName;

        /// <summary>
        /// Column name of the parent key in this table, or null if this is a main table.
        /// </summary>
        public string ParentKeyColumnName;

        /// <summary>
        /// The SQL table name (which is often the same as Name)
        /// </summary>
        public string SqlTableName; 

        /// <summary>
        /// Table prompt in natural language indexed by language code (index is "" for default language);
        /// or null to fall back to table name as the prompt
        /// </summary>
        public SortedList<string, string> Prompt;

        public bool HasCustomColumns { get; private set; }

        /// <summary>
        /// Convenience method to get the column by name or null 
        /// </summary>
        public ColDef FindCol(string name, bool caseSensitive = true)
        {
            if (caseSensitive) return Cols.FirstOrDefault(c => c.Name == name);
            return Cols.FirstOrDefault(c => string.Compare(c.Name, name, true, CultureInfo.InvariantCulture) == 0);
        }

        /// <summary>
        /// Convenience method to get the child table by name or null 
        /// </summary>
        public TableDef FindChildTable(string name) => Children?.FirstOrDefault(c => c.Name == name);

        /// <summary>
        /// Add a custom column to the table
        /// </summary>
        public ColDef AddCustomColum(string name, Type cstype, string wireType)
        {
            var coldef = new ColDef
            {
                IsCustom = true,
                Name = name,
                CSType = cstype,
                WireType = wireType
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
