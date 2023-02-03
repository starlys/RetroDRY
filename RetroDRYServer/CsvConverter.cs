using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Facility to convert a daton into a CSV stream
    /// </summary>
    public class CsvConverter
    {
        /// <summary>
        /// A normal entry has only ColDef defined. When we add a second column to output the display value for a FK value,
        /// then the entry has the ColDef for the same field, and LookupDatonKey defines which daton holds the display values
        /// </summary>
        class OutputEntry
        {
            public ColDef ColDef;
            public DatonKey? LookupDatonKey;

            public OutputEntry(ColDef c) { ColDef = c; }
        }

        readonly DatonDef DatonDef;
        readonly TableDef MainTableDef;
        readonly LookupResolver LookupResolver;
        readonly List<OutputEntry> OutputEntries = new List<OutputEntry>();

        /// <summary>
        /// Construct
        /// </summary>
        /// <param name="datonDef"></param>
        /// <param name="lookupResolver">any instance, which this converter can use over its lifetime</param>
        /// <param name="dict"></param>
        public CsvConverter(DataDictionary dict, LookupResolver lookupResolver, DatonDef datonDef)
        {
            DatonDef = datonDef;
            LookupResolver = lookupResolver;
            MainTableDef = datonDef.MainTableDef ?? throw new Exception("Main table not defined in CsvConverter");

            //set up cols (including additional ones for display values)
            foreach (var coldef in MainTableDef.Cols)
            {
                //add this column for db values
                OutputEntries.Add(new OutputEntry(coldef));

                //add a separate column for the resolved display value of foreign keys
                if (coldef.ForeignKeyDatonTypeName != null)
                {
                    var foreignDatondef = dict.FindDef(coldef.ForeignKeyDatonTypeName);
                    if (foreignDatondef != null && foreignDatondef.MainTableDef != null && foreignDatondef.MultipleMainRows)
                    {
                        var keyForSourceRows = new PersistonKey(foreignDatondef.Type.Name, null, true);
                        OutputEntries.Add(new OutputEntry(coldef) {  LookupDatonKey = keyForSourceRows });
                    }
                }
            }
        }

        /// <summary>
        /// Write column names row
        /// </summary>
        /// <param name="wri"></param>
        public void WriteHeaderRow(StreamWriter wri)
        {
            int displayNameSuffix = 0;
            wri.Write("_RowNumber");
            foreach (var entry in OutputEntries)
            {
                string fixedName = Regex.Replace(entry.ColDef.FieldName, @"[\s""]+", "_");
                wri.Write(',');
                wri.Write(fixedName);

                //for display value cols, we append _D1, _D2 etc to the name
                if (entry.LookupDatonKey != null) wri.Write($"_D{++displayNameSuffix}");
            }
            wri.WriteLine();
            wri.Flush();
        }

        /// <summary>
        /// Write a CSV representation of the main table of the given daton to the StreamWriter, excluding header row
        /// </summary>
        /// <param name="daton"></param>
        /// <param name="wri"></param>
        public async Task WriteAllRows(Daton daton, StreamWriter wri)
        {
            if (DatonDef.MultipleMainRows)
            {
                if (!(RecurPoint.GetMainTable(DatonDef, daton) is IList rows)) return;
                int rowNo = 0;
                foreach (Row row in rows) await WriteRow(row, ++rowNo, wri);
            }
            else await WriteRow(daton, 1, wri);
            await wri.FlushAsync();
        }

        /// <summary>
        /// Write a single CSV row; does not flush
        /// </summary>
        /// <param name="row"></param>
        /// <param name="rowNo">1-based row number that gets written as first column</param>
        /// <param name="wri"></param>
        public async Task WriteRow(Row row, int rowNo, StreamWriter wri)
        {
            wri.Write(rowNo);
            foreach (var entry in OutputEntries)
            {
                var value = row.GetValue(entry.ColDef);
                wri.Write(',');
                if (entry.LookupDatonKey == null)
                    wri.Write(Retrovert.FormatRawExportValue(entry.ColDef, value));
                else
                    wri.Write(await LookupResolver.DisplayValueFor(entry.LookupDatonKey, value));
            }
            await wri.WriteLineAsync(); //possibly this will allow large tables to stream to the client while this is looping the rows
        }
    }
}
