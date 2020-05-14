using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RetroDRY
{
    /// <summary>
    /// Base class for PersistonKey and ViewonKey
    /// </summary>
    public abstract class DatonKey
    {
        public const string DELIMITER = "|", ESCAPE = @"\", ESCAPEDDELIMITER = ESCAPE + DELIMITER, ESCAPEDESCAPE = ESCAPE + ESCAPE;

        /// <summary>
        /// The value used for PersistonKey.PrimaryKey when the persiston is new and unsaved
        /// </summary>
        public const string NEWPK = "-1";

        public readonly string Name;

        protected DatonKey(string name) { Name = name; }

        /// <summary>
        /// True if this is a key for a new, unsaved persiston
        /// </summary>
        public virtual bool IsNew => false;

        public static DatonKey Parse(string s)
        {
            var segments = ParseSegments(s);
            if (segments.Count < 1) throw new Exception("Misformed daton key: " + s);

            //check if it is a persiston
            if (segments.Count == 2)
            {
                string keyseg = segments[1];
                if (keyseg.StartsWith("=")) return new PersistonKey(segments[0], keyseg.Substring(1), false);
                if (keyseg.StartsWith("+")) return new PersistonKey(segments[0], null, true);
            }

            //else it is a viewon
            return new ViewonKey(segments);
        }

        protected static string Escape(string s) => s.Replace(ESCAPE, ESCAPEDESCAPE).Replace(DELIMITER, ESCAPEDDELIMITER);

        /// <summary>
        /// Convert string form of daton key into unescaped segments
        /// </summary>
        protected static List<string> ParseSegments(string s)
        {
            var segments = s.Split(DELIMITER[0]).ToList();

            //unescape; and
            //in case any of the segments contained \|, it would be misinterpreted as 2 segments, so fix that
            for (int i = segments.Count - 1; i >= 0; --i) 
            {
                string segi = segments[i].Replace(ESCAPEDESCAPE, "\x1");
                if (segi.EndsWith(ESCAPE))
                {
                    segments[i] = segi.Substring(0, segi.Length - 1) + segments[i + 1];
                    segments.RemoveAt(i + 1);
                }
                segments[i] = segments[i].Replace("\x1", ESCAPE);
            }

            return segments;
        }
    }

    /// <summary>
    /// Immutable key of a persiston (suitable for use as hash index)
    /// </summary>
    public class PersistonKey : DatonKey
    {
        public readonly string PrimaryKey;
        public readonly bool WholeTable; //if true, then primarykey is ignored
        
        public PersistonKey(string name, string primaryKey, bool wholeTable) : base(name)
        {
            PrimaryKey = primaryKey;
            WholeTable = wholeTable;
        }

        public override bool IsNew => !WholeTable && PrimaryKey == NEWPK;

        public override string ToString()
        {
            if (WholeTable)
                return Name + "|+";
            else
                return Name + DELIMITER + "=" + Escape(PrimaryKey);
        }

        public override bool Equals(object obj)
        {
            return ((obj is PersistonKey k)
                && k.WholeTable == WholeTable
                && k.PrimaryKey == PrimaryKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Name.GetHashCode() + PrimaryKey.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Immutable key of a viewon (suitable for use as hash index)
    /// </summary>
    public class ViewonKey : DatonKey
    {
        public const string SORTID = "_sort", PAGEID = "_page";

        public class Criterion
        {
            public readonly string Name;

            /// <summary>
            /// The value as defined for segments in viewon keys; use ViewonCriterion class to work with these values
            /// </summary>
            public readonly string PackedValue;

            public Criterion(string n, string val)
            {
                Name = n; PackedValue = val;
            }
        }

        /// <summary>
        /// These are not alphabetized; can be null
        /// </summary>
        private readonly Criterion[] _Criteria;

        public IEnumerable<Criterion> Criteria => _Criteria;

        public readonly string SortColumnName;

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
                string nam = segment.Substring(0, equalsIdx), val = segment.Substring(equalsIdx + 1);

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
        public ViewonKey(string viewonName, IEnumerable<Criterion> criteria = null, string sortColumnName = null, int pageNo = 0) : base(viewonName)
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

        public override string ToString()
        {
            return AsString;
        }

        public override bool Equals(object obj)
        {
            return ((obj is ViewonKey k)
                && k.AsString == AsString);
        }

        public override int GetHashCode()
        {
            return AsString.GetHashCode();
        }
    }
}
