using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroDRY
{
    /// <summary>
    /// Base class for PersistonKey and ViewonKey
    /// </summary>
    public abstract class DatonKey
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected const string DELIMITER = "|", ESCAPE = @"\", ESCAPEDDELIMITER = ESCAPE + DELIMITER, ESCAPEDESCAPE = ESCAPE + ESCAPE;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        /// <summary>
        /// The value used for PersistonKey.PrimaryKey when the persiston is new and unsaved
        /// </summary>
        public const string NEWPK = "-1";

        /// <summary>
        /// Daton name
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Create with a name only, no value
        /// </summary>
        protected DatonKey(string name) { Name = name; }

        /// <summary>
        /// True if this is a key for a new, unsaved persiston
        /// </summary>
        public virtual bool IsNew => false;

        /// <summary>
        /// Parse a string daton key and return a ViewonKey or PersistonKey
        /// </summary>
        public static DatonKey Parse(string? s)
        {
            var segments = ParseSegments(s);
            if (segments.Count < 1) throw new Exception("Misformed daton key: " + s);

            //check if it is a persiston
            if (segments.Count == 2)
            {
                string keyseg = segments[1];
                if (keyseg.StartsWith("=")) return new PersistonKey(segments[0], keyseg[1..], false);
                if (keyseg.StartsWith("+")) return new PersistonKey(segments[0], null, true);
            }

            //else it is a viewon
            return new ViewonKey(segments);
        }

        /// <summary>
        /// Return escaped string segment; use this only for the values within the full key, not for the key as a whole
        /// </summary>
        protected static string Escape(string? s)
        {
            if (s == null) return "";
            return s.Replace(ESCAPE, ESCAPEDESCAPE).Replace(DELIMITER, ESCAPEDDELIMITER);
        }

        /// <summary>
        /// Convert string form of daton key into unescaped segments
        /// </summary>
        protected static List<string> ParseSegments(string? s)
        {
            var segments = (s ?? "").Split(DELIMITER[0]).ToList();

            //unescape; and
            //in case any of the segments contained \|, it would be misinterpreted as 2 segments, so fix that
            for (int i = segments.Count - 1; i >= 0; --i) 
            {
                string segi = segments[i].Replace(ESCAPEDESCAPE, "\x1");
                if (segi.EndsWith(ESCAPE))
                {
                    segi = segi[..^1] + "|" + segments[i + 1];
                    segments.RemoveAt(i + 1);
                }
                segments[i] = segi.Replace("\x1", ESCAPE);
            }

            return segments;
        }
    }

    /// <summary>
    /// Immutable key of a persiston (suitable for use as hash index)
    /// </summary>
    public class PersistonKey : DatonKey
    {
        /// <summary>
        /// Primary key of the persiston's main single row; also see WholeTable
        /// </summary>
        public readonly string? PrimaryKey;

        /// <summary>
        /// if true, then primarykey is ignored
        /// </summary>
        public readonly bool WholeTable;
        
        /// <summary>
        /// Create
        /// </summary>
        /// <param name="name">daton type name</param>
        /// <param name="primaryKey">primary key value</param>
        /// <param name="wholeTable">see WholeTable</param>
        public PersistonKey(string name, string? primaryKey, bool wholeTable) : base(name)
        {
            PrimaryKey = primaryKey;
            WholeTable = wholeTable;
        }

        /// <summary>
        /// True if persiston has never been persisted
        /// </summary>
        public override bool IsNew => !WholeTable && PrimaryKey == NEWPK;

        /// <summary>
        /// Get the string representation of the persiston key
        /// </summary>
        public override string ToString()
        {
            if (WholeTable)
                return Name + "|+";
            else
                return Name + DELIMITER + "=" + Escape(PrimaryKey);
        }

        /// <summary>
        /// true if obj is a PersistonKey with the same value
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return ((obj is PersistonKey k)
                && k.WholeTable == WholeTable
                && k.PrimaryKey == PrimaryKey);
        }

        /// <summary>
        /// Hash code of the daton name and primary key
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = Name.GetHashCode();
                if (PrimaryKey != null) h += PrimaryKey.GetHashCode();
                return h;
            }
        }
    }

    /// <summary>
    /// Immutable key of a viewon (suitable for use as hash index)
    /// </summary>
    public class ViewonKey : DatonKey
    {
        /// <summary>
        /// name embedded in key string for storing the sort col name
        /// </summary>
        public const string SORTID = "_sort", PAGEID = "_page";

        /// <summary>
        /// A single criteron with value
        /// </summary>
        public class Criterion
        {
            /// <summary>
            /// Column/criterion name
            /// </summary>
            public readonly string Name;

            /// <summary>
            /// The value as defined for segments in viewon keys; use ViewonCriterion class to work with these values
            /// </summary>
            public readonly string PackedValue;

            /// <summary>
            /// Create
            /// </summary>
            /// <param name="name"></param>
            /// <param name="val">must be the packed value</param>
            public Criterion(string name, string val)
            {
                Name = name; PackedValue = val;
            }
        }

        /// <summary>
        /// These are not alphabetized; can be null
        /// </summary>
        private readonly Criterion[]? _Criteria;

        /// <summary>
        /// May be null
        /// </summary>
        public IEnumerable<Criterion>? Criteria => _Criteria;

        /// <summary>
        /// Name of column for order-by clause
        /// </summary>
        public readonly string? SortColumnName;

        /// <summary>
        /// 0-based page number
        /// </summary>
        public readonly int PageNumber;

        /// <summary>
        /// The string version; note that since this is immutable, only the constructors do work and store the info both ways 
        /// </summary>
        public readonly string AsString;

        /// <summary>
        /// Construct using partially parsed string of unescaped segments (for framework use mainly)
        /// </summary>
        /// <param name="segments">list of segments in the form a=b</param>
        public ViewonKey(List<string> segments) : base(segments[0])
        {
            var cris = new List<Criterion>();
            foreach (string segment in segments.Skip(1))
            {
                int equalsIdx = segment.IndexOf('=');
                if (equalsIdx < 0) continue;
                string nam = segment[..equalsIdx], val = segment[(equalsIdx + 1)..];

                //this segment could be a criteria or sort order
                if (nam == SORTID)
                    SortColumnName = val;
                else if (nam == PAGEID)
                    int.TryParse(val, out PageNumber);
                else
                    cris.Add(new Criterion(nam, val));
            }
            _Criteria = cris.ToArray();

            AsString = BuildStringRepresentation();
        }

        /// <summary>
        /// Construct using optional criteria and optional sort order/page
        /// </summary>
        /// <param name="criteria">values indexed by column name; each value has to be inthe packed format defined by ViewonCriterion</param>
        /// <param name="pageNo">0-baed page of results</param>
        /// <param name="sortColumnName">column name for order-by clause</param>
        /// <param name="viewonName">name of Daton type</param>
        public ViewonKey(string viewonName, IEnumerable<Criterion>? criteria = null, string? sortColumnName = null, int pageNo = 0) : base(viewonName)
        {
            SortColumnName = sortColumnName;
            PageNumber = pageNo;
            if (criteria == null || criteria.Count() == 0)
                _Criteria = null;
            else
                _Criteria = criteria.ToArray();

            AsString = BuildStringRepresentation();
        }

        private string BuildStringRepresentation()
        {
            //the string version must be alphabetized per documentation
            var segments = new List<string>();
            if (_Criteria != null)
                foreach (var cri in _Criteria)
                    segments.Add(cri.Name + "=" + Escape(cri.PackedValue));
            if (!string.IsNullOrEmpty(SortColumnName))
                segments.Add($"{SORTID}={SortColumnName}");
            if (PageNumber != 0)
                segments.Add($"{PAGEID}={PageNumber}");
            segments.Sort();

            if (segments.Any())
                return Name + DELIMITER + string.Join(DELIMITER, segments);
            return Name;
        }

        /// <summary>
        /// Get the string representation of the viewon key
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return AsString;
        }

        /// <summary>
        /// True if obj is a ViewonKey with the same string representation
        /// </summary>
        /// <param name="obj"></param>
        public override bool Equals(object obj)
        {
            return ((obj is ViewonKey k)
                && k.AsString == AsString);
        }

        /// <summary>
        /// hash code of string representation
        /// </summary>
        public override int GetHashCode()
        {
            return AsString.GetHashCode();
        }
    }
}
