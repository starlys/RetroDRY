using System;
using System.Collections;

#pragma warning disable IDE0019

namespace RetroDRY
{
    /// <summary>
    /// Base class for Persiston and Viewon
    /// </summary>
    public abstract class Daton : Row
    {
        /// <summary>
        /// Key identifying daton
        /// </summary>
        public DatonKey? Key { get; set; }

        /// <summary>
        /// null if this is newly created and never saved, else the version matching the RetroLock table
        /// (Used only in persistons but declared at base level for easier coding of serialize and other conversions)
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Clone all fields and child tables declared in datondef
        /// </summary>
        public virtual Daton Clone(DatonDef datondef)
        {
            if (datondef.MainTableDef == null) throw new Exception("Expected MainTableDef in Clone");
            if (datondef.MultipleMainRows)
            {
                var target = Utils.Construct(datondef.Type) as Daton;
                if (target == null) throw new Exception("Failed to construct daton in Clone");
                target.Key = Key;
                target.Version = Version;
                var listField = datondef.Type.GetField(datondef.MainTableDef.Name);
                var sourceList = listField.GetValue(this) as IList;
                var targetList = listField.GetValue(target) as IList;
                if (sourceList != null && targetList != null)
                {
                    if (targetList == null)
                    {
                        targetList = Utils.Construct(listField.FieldType) as IList;
                        listField.SetValue(target, targetList);
                    }
                    foreach (var row in sourceList)
                        if (row is Row trow)
                            targetList?.Add(trow.Clone(datondef.MainTableDef));
                }
                return target;
            }
            else //single main row
            {
                var target = Clone(datondef.MainTableDef) as Daton;
                if (target == null) throw new Exception("Cannot clone daton");
                target.Key = Key;
                target.Version = Version;
                return target;
            }                
        }

        /// <summary>
        /// Calls Recompute on each row in the daton
        /// </summary>
        public void Recompute(DatonDef datondef)
        {
            var r = RecurPoint.FromDaton(datondef, this);
            if (r is RowRecurPoint rr) Recompute(rr);
            else if (r is TableRecurPoint rt) Recompute(rt);
        }

        private void Recompute(TableRecurPoint rt)
        {
            foreach (var rr in rt.GetRows()) Recompute(rr);
        }

        private void Recompute(RowRecurPoint rr)
        {
            rr.Row.Recompute(this);
            foreach (var rt in rr.GetChildren()) Recompute(rt);
        }
    }
}
