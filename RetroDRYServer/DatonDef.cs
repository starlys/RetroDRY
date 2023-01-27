using System;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Definition of one daton type
    /// </summary>
    public class DatonDef
    {
        /// <summary>
        /// Daton subclass
        /// </summary>
        public Type Type;

        /// <summary>
        /// Definition of main table
        /// </summary>
        public TableDef MainTableDef;

        /// <summary>
        /// For viewons, this may be set to a quasi-table whose columns define the criteria. Otherwise null.
        /// </summary>
        public TableDef? CriteriaDef;

        /// <summary>
        /// If true, the daton subclass should contain only one or more Lists of rows using nested types; if false,
        /// the daton subclass should declare the main row fields at the top level
        /// </summary>
        public bool MultipleMainRows;

        /// <summary>
        /// The database number where this daton is stored (always 0 unless this is a larger scaled application)
        /// </summary>
        public int DatabaseNumber;

        /// <summary>
        /// Injectable initializer of new datons
        /// </summary>
        public Func<Daton, Task>? Initializer;

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="type"></param>
        /// <param name="mainTableDef"></param>
        public DatonDef(Type type, TableDef mainTableDef)
        {
            Type = type;
            MainTableDef = mainTableDef;
        }
    }
}
