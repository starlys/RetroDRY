using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace RetroDRY
{
    /// <summary>
    /// Base class for the functionality to recurse over all rows and child tables in a daton.
    /// </summary>
    public abstract class RecurPoint
    {
        /// <summary>
        /// Definition of table being traveled
        /// </summary>
        public TableDef TableDef;

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="tableDef"></param>
        protected RecurPoint(TableDef tableDef)
        {
            TableDef = tableDef;
        }

        /// <summary>
        /// Get a RecurPoint for the top level of a daton, which will be a RowRecurPoint for single-main-row type, else a TableRecurPoint
        /// </summary>
        public static RecurPoint FromDaton(DatonDef datondef, Daton daton)
        {
            if (datondef.MainTableDef == null) throw new Exception("Expected main table to be defined in RecurPoint");
            if (datondef.MultipleMainRows)
                return new TableRecurPoint(datondef.MainTableDef, GetMainTable(datondef, daton, createIfMissing: true)!);
            else
                return new RowRecurPoint(datondef.MainTableDef, daton);
        }

        /// <summary>
        /// Get the main table (IList) of a daton
        /// </summary>
        public static IList? GetMainTable(DatonDef datondef, Daton daton, bool createIfMissing = false)
        {
            if (datondef.MainTableDef == null) throw new Exception("Expected main table to be defined in RecurPoint");
            var f = daton.GetType().GetField(datondef.MainTableDef.Name);
            return GetChildTable(daton, f, createIfMissing);
        }

        /// <summary>
        /// Get a child table (IList) within a parent Row using the child's TableDef.
        /// </summary>
        public static IList? GetChildTable(TableDef parentdef, Row parent, TableDef memberdef, bool createIfMissing = false)
        {
            if (parent.GetType() != parentdef.RowType) throw new Exception("Incorrect type");
            var f = parentdef.RowType.GetField(memberdef.Name) 
                ?? throw new Exception($"{memberdef.Name} not found in {parentdef.Name}");
            return GetChildTable(parent, f, createIfMissing);
        }

        /// <summary>
        /// Get a child table (IList) within a parent Row using the child's FieldInfo
        /// </summary>
        private static IList? GetChildTable(Row parent, FieldInfo f, bool createIfMissing)
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
        /// <summary>
        /// table rows
        /// </summary>
        public IList Table;

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="table"></param>
        /// <param name="tableDef"></param>
        public TableRecurPoint(TableDef tableDef, IList table) : base(tableDef)
        {
            Table = table;
        }


        /// <summary>
        /// Convert table rows into RowRecurPoints
        /// </summary>
        public IEnumerable<RowRecurPoint> GetRows()
        {
            foreach (Row row in Table)
                yield return new RowRecurPoint(TableDef, row);
        }
    }

    /// <summary>
    /// RecurPoint for a row
    /// </summary>
    public class RowRecurPoint : RecurPoint
    {
        /// <summary>
        /// The row this relates to
        /// </summary>
        public Row Row;

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="tableDef"></param>
        /// <param name="row"></param>
        public RowRecurPoint(TableDef tableDef, Row row) : base(tableDef)
        {
            Row = row;
        }

        /// <summary>
        /// Get the primary key value for the row
        /// </summary>
        public object GetPrimaryKey()
        {
            var pkField = GetPrimaryKeyField();
            var pkValue = pkField.GetValue(Row);
            return pkValue;
        }

        /// <summary>
        /// Set the primary key value for the row
        /// </summary>
        public void SetPrimaryKey(object value)
        {
            var pkField = GetPrimaryKeyField();
            pkField.SetValue(Row, value);
        }

        private FieldInfo GetPrimaryKeyField()
        {
            var pkField = TableDef.RowType.GetField(TableDef.PrimaryKeyFieldName)
                ?? throw new Exception($"{TableDef.PrimaryKeyFieldName} not found in {TableDef.Name}");
            return pkField;
        }

        /// <summary>
        /// Get the collection of TableRecurPoints for the child-row collections of this row
        /// </summary>
        public IEnumerable<TableRecurPoint> GetChildren()
        {
            if (TableDef.Children != null)
            {
                foreach (var childTableDef in TableDef.Children)
                {
                    yield return new TableRecurPoint(childTableDef, GetChildTable(TableDef, Row, childTableDef, createIfMissing: true)!);
                }
            }
        }
    }
}
