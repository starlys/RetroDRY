using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// Facility to convert a daton into a CSV stream
    /// </summary>
    public static class CsvConverter
    {
        /// <summary>
        /// Write a CSV representation of the main table of the given daton to the StreamWriter
        /// </summary>
        /// <param name="datondef"></param>
        /// <param name="daton"></param>
        /// <param name="wri"></param>
        public static async Task Convert(DatonDef datondef, Daton daton, StreamWriter wri)
        {
            if (datondef.MainTableDef == null) return;

            //write col names
            wri.Write("_RowNumber");
            foreach (var coldef in datondef.MainTableDef.Cols)
            {
                string fixedName = Regex.Replace(coldef.FieldName, @"[\s""]+", "_");
                wri.Write(',');
                wri.Write(fixedName);
            }
            wri.WriteLine();
            wri.Flush();

            //collect FieldInfos
            //var infos = datondef.MainTableDef.Cols.Select(c => c.)

            //write rows
            var rows = RecurPoint.GetMainTable(datondef, daton);
            if (rows == null) return;
            int rowNo = 0;
            foreach (Row row in rows)
            {
                wri.Write(++rowNo);
                foreach (var coldef in datondef.MainTableDef.Cols)
                {
                    var value = row.GetValue(coldef);
                    wri.Write(',');
                    wri.Write(Retrovert.FormatRawExportValue(coldef, value));
                }
                await wri.WriteLineAsync(); //possibly this will allow large tables to stream to the client while this is looping the rows
            }

            await wri.FlushAsync();
        }
    }
}
