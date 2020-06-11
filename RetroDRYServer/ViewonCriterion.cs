using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RetroDRY
{
    /// <summary>
    /// Storage for viewon criterion with helper methods for dealing with SQL selects.
    /// </summary>
    public class ViewonCriterion
    {
        public readonly ColDef ColDef;

        /// <summary>
        /// As defined in documentation (for example, numerics require a hyphen, so don't just store a number here)
        /// </summary>
        public readonly string PackedValue;

        public ViewonCriterion(ColDef c, string packedValue)
        {
            ColDef = c; PackedValue = packedValue;
        }

        /// <summary>
        /// Create from viewon key
        /// </summary>
        public ViewonCriterion(TableDef tabledef, ViewonKey.Criterion cri)
        {
            ColDef = tabledef.FindCol(cri.Name);
            if (ColDef == null) throw new Exception($"No such name {cri.Name} in data dictionary");
            PackedValue = cri.PackedValue;
        }

        public void ExportWhereClause(SqlSelectBuilder.Where w)
        {
            //numeric ranges
            if (Utils.IsSupportedNumericType(ColDef.CSType))
            {
                try
                {
                    (string lo, string hi) = SplitOnTilde(PackedValue);
                    if (lo != null)
                    {
                        decimal dlo = decimal.Parse(lo);
                        w.AddWhere($"{ColDef.Name}>={w.NextParameterName()}", dlo);
                    }
                    if (hi != null)
                    {
                        decimal dhi = decimal.Parse(hi);
                        w.AddWhere($"{ColDef.Name}<={w.NextParameterName()}", dhi);
                    }
                }
                catch
                {
                    throw new Exception($"Misformatted numeric parameter: {PackedValue}");
                }
            }

            //dates and times
            else if (ColDef.CSType == typeof(DateTime))
            {
                bool isDateOnly = ColDef.WireType == Constants.TYPE_DATE;
                (string lo, string hi) = SplitOnTilde(PackedValue);
                if (lo != null)
                {
                    var dlo = ParseDateTimeCriterion(lo, isDateOnly); 
                    w.AddWhere($"{ColDef.Name}>={w.NextParameterName()}", dlo);
                }
                if (hi != null)
                {
                    var dhi = ParseDateTimeCriterion(hi, isDateOnly);
                    w.AddWhere($"{ColDef.Name}<={w.NextParameterName()}", dhi);
                }
            }

            else if (ColDef.CSType == typeof(bool))
            {
                bool b;
                if (PackedValue == "0") b = false;
                else if (PackedValue == "1") b = true;
                else throw new Exception($"Boolean parameter must be 0 or 1: {PackedValue}");
                w.AddWhere($"{ColDef.Name}={w.NextParameterName()}", b);
            }

            else if (ColDef.CSType == typeof(string))
            {
                w.AddWhere($"{ColDef.Name} like {w.NextParameterName()}", PackedValue + "%");
            }

            else throw new Exception($"Type {ColDef.CSType.Name} not supported as a viewon parameter");
        }

        /// <summary>
        /// Split a string in the form "1~2", "~2", or "1~" into the low and high parts of the range, ensuring nulls
        /// are returned if no values provided.
        /// </summary>
        public static (string, string) SplitOnTilde(string s)
        {
            int h = s.IndexOf('~');
            if (h < 0) return(null, null);
            string lo = s.Substring(0, h), hi = s.Substring(h + 1);
            if (lo.Length == 0) lo = null;
            if (hi.Length == 0) hi = null;
            return (lo, hi);
        }

        /// <summary>
        /// Given a packed criterion date or datetime, convert to a DateTIme instance or throw exception
        /// </summary>
        public static DateTime ParseDateTimeCriterion(string s, bool isDateOnly)
        {
            try
            {
                int yr = int.Parse(s.Substring(0, 4));
                int mo = int.Parse(s.Substring(4, 2));
                int da = int.Parse(s.Substring(6, 2));
                if (isDateOnly || s.Length == 8) return new DateTime(yr, mo, da, 0, 0, 0, DateTimeKind.Utc);
                int hr = int.Parse(s.Substring(8, 2));
                int mi = int.Parse(s.Substring(10, 2));
                return new DateTime(yr, mo, da, hr, mi, 0, DateTimeKind.Utc);
            }
            catch
            {
                throw new Exception($"Datetime criterion {s} is misformatted; expected YYYYMMDD or YYYYMMDDHHMM");
            }
        }
    }
}
