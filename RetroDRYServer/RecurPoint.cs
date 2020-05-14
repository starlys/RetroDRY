using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace RetroDRY
{
    /// <summary>
    /// Base class for the functionality to recurse over all rows and child tables in a daton.
    /// </summary>
    public class RecurPoint
    {
        public TableDef TableDef;

        /// <summary>
        /// Get a RecurPoint for the top level of a daton, which will be a RowRecurPoint for single-main-row type, else a TableRecurPoint
        /// </summary>
        public static RecurPoint FromDaton(DatonDef datondef, Daton daton)
        {
            if (datondef.MultipleMainRows)
                return new TableRecurPoint { TableDef = datondef.MainTableDef, Table = GetMainTable(datondef, daton, createIfMissing: true) };
            else
                return new RowRecurPoint { TableDef = datondef.MainTableDef, Row = daton };
        }

        public static IList GetMainTable(DatonDef datondef, Daton daton, bool createIfMissing = false)
        {
            var f = daton.GetType().GetField(datondef.MainTableDef.Name);
            return GetChildTable(daton, f, createIfMissing);
        }

        /// <summary>
        /// Get a child table (IList) within a parent Row using the child's TableDef.
        /// </summary>
        public static IList GetChildTable(TableDef parentdef, Row parent, TableDef memberdef, bool createIfMissing = false)
        {
            if (parent.GetType() != parentdef.RowType) throw new Exception("Incorrect type");
            var f = parentdef.RowType.GetField(memberdef.Name); 
            if (f == null) throw new Exception($"{memberdef.Name} not found in {parentdef.Name}");
            return GetChildTable(parent, f, createIfMissing);
        }

        /// <summary>
        /// Get a child table (IList) within a parent Row using the child's FieldInfo
        /// </summary>
        private static IList GetChildTable(Row parent, FieldInfo f, bool createIfMissing)
        {
            var list = f.GetValue(parent) as IList;
            if (list == null && createIfMissing)
            {
                list = Utils.Construct(f.FieldType) as IList;
                f.SetValue(parent, list);
            }
            return list;
        }
    }

    /// <summary>
    /// RecurPoint for a table
    /// </summary>
    public class TableRecurPoint : RecurPoint
    {
        public IList Table;
    
        public IEnumerable<RowRecurPoint> GetRows()
        {
            foreach (Row row in Table)
                yield return new RowRecurPoint
                {
                    TableDef = TableDef,
                    Row = row
                };
        }
    }

    /// <summary>
    /// RecurPoint for a row
    /// </summary>
    public class RowRecurPoint : RecurPoint
    {
        public Row Row;

        public object GetPrimaryKey()
        {
            var pkField = GetPrimaryKeyField();
            var pkValue = pkField.GetValue(Row);
            return pkValue;
        }

        public void SetPrimaryKey(object value)
        {
            var pkField = GetPrimaryKeyField();
            pkField.SetValue(Row, value);
        }

        private FieldInfo GetPrimaryKeyField()
        {
            var pkField = TableDef.RowType.GetField(TableDef.PrimaryKeyColName);
            if (pkField == null) throw new Exception($"{TableDef.PrimaryKeyColName} not found in {TableDef.Name}");
            return pkField;
        }

        public IEnumerable<TableRecurPoint> GetChildren()
        {
            if (TableDef.Children != null)
            {
                foreach (var childTableDef in TableDef.Children)
                {
                    yield return new TableRecurPoint
                    {
                        TableDef = childTableDef,
                        Table = GetChildTable(TableDef, Row, childTableDef, createIfMissing: true)
                    };
                }
            }
        }
    }
}
