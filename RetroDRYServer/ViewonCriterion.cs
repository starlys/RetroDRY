﻿using System;

namespace RetroDRY
{
    /// <summary>
    /// Storage for viewon criterion with helper methods for dealing with SQL selects.
    /// </summary>
    public class ViewonCriterion
    {
        /// <summary>
        /// Definition of criterion
        /// </summary>
        public readonly ColDef ColDef;

        /// <summary>
        /// As defined in documentation (for example, numerics require a hyphen, so don't just store a number here)
        /// </summary>
        public readonly string PackedValue;

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="c">criteron definition </param>
        /// <param name="packedValue">round tripped value format</param>
        public ViewonCriterion(ColDef c, string packedValue)
        {
            ColDef = c; PackedValue = packedValue;
        }

        /// <summary>
        /// Modify where clause to add conditions for each criterion
        /// </summary>
        /// <param name="sql">the calling RetroSql, which may override where-clause behavior</param>
        /// <param name="w"></param>
        /// <param name="sqlFlavor"></param>
        public void ExportWhereClause(RetroSql sql, SqlSelectBuilder.Where w, SqlFlavorizer sqlFlavor)
        {
            //numeric ranges
            if (Utils.IsSupportedNumericType(ColDef.CSType))
            {
                try
                {
                    (string? lo, string? hi) = SplitOnTilde(PackedValue);
                    if (lo != null)
                    {
                        decimal dlo = decimal.Parse(lo);
                        sql.CustomizeWhereClause(w, $"{ColDef.SqlExpression}>={w.NextParameterName()}", dlo);
                    }
                    if (hi != null)
                    {
                        decimal dhi = decimal.Parse(hi);
                        sql.CustomizeWhereClause(w, $"{ColDef.SqlExpression}<={w.NextParameterName()}", dhi);
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
                (string? lo, string? hi) = SplitOnTilde(PackedValue);
                if (lo != null)
                {
                    var dlo = Retrovert.ParseRetroDateTime(lo, isDateOnly); 
                    sql.CustomizeWhereClause(w, $"{ColDef.SqlExpression}>={w.NextParameterName()}", dlo);
                }
                if (hi != null)
                {
                    var dhi = Retrovert.ParseRetroDateTime(hi, isDateOnly);
                    sql.CustomizeWhereClause(w, $"{ColDef.SqlExpression}<={w.NextParameterName()}", dhi);
                }
            }

            else if (ColDef.CSType == typeof(bool))
            {
                bool b;
                if (PackedValue == "0") b = false;
                else if (PackedValue == "1") b = true;
                else throw new Exception($"Boolean parameter must be 0 or 1: {PackedValue}");
                sql.CustomizeWhereClause(w, $"{ColDef.SqlExpression}={w.NextParameterName()}", b);
            }

            else if (ColDef.CSType == typeof(string))
            {
                sql.CustomizeWhereClause(w, $"{ColDef.SqlExpression} {sqlFlavor.LikeOperator()} {w.NextParameterName()}", sqlFlavor.LikeParamValue(PackedValue));
            }

            else throw new Exception($"Type {ColDef.CSType.Name} not supported as a viewon parameter");
        }

        /// <summary>
        /// Split a string in the form "1~2", "~2", "1~" or "1" into the low and high parts of the range, ensuring nulls
        /// are returned if no values provided. If no tilde is in the string, it returns the same value for lo and hi.
        /// </summary>
        public static (string?, string?) SplitOnTilde(string s)
        {
            int h = s.IndexOf('~');
            if (h < 0) return(s, s);
            string? lo = s[..h], hi = s[(h + 1)..];
            if (lo.Length == 0) lo = null;
            if (hi.Length == 0) hi = null;
            return (lo, hi);
        }
    }
}
