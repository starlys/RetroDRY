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
        /// <param name="wri">this will only write async to the writer</param>
        public async Task WriteHeaderRow(StreamWriter wri)
        {
            int displayNameSuffix = 0;
            await wri.WriteAsync("_RowNumber");
            foreach (var entry in OutputEntries)
            {
                string fixedName = Regex.Replace(entry.ColDef.FieldName, @"[\s""]+", "_");
                await wri.WriteAsync(',');
                await wri.WriteAsync(fixedName);

                //for display value cols, we append _D1, _D2 etc to the name
                if (entry.LookupDatonKey != null) await wri.WriteAsync($"_D{++displayNameSuffix}");
            }
            await wri.WriteLineAsync();
        }

        /// <summary>
        /// Write a CSV representation of the main table of the given daton to the StreamWriter, excluding header row
        /// </summary>
        /// <param name="daton"></param>
        /// <param name="wri">this will only write async to the writer</param>
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
        /// <param name="wri">this will only write async to the writer</param>
        public async Task WriteRow(Row row, int rowNo, StreamWriter wri)
        {
            await wri.WriteAsync(rowNo.ToString());
            foreach (var entry in OutputEntries)
            {
                var value = row.GetValue(entry.ColDef);
                await wri.WriteAsync(',');
                if (entry.LookupDatonKey == null)
                    await wri.WriteAsync(Retrovert.FormatCsvExportValue(entry.ColDef, value));
                else
                    await wri.WriteAsync(await LookupResolver.DisplayValueFor(entry.LookupDatonKey, value));
            }
            await wri.WriteLineAsync(); 
        }
    }
}
