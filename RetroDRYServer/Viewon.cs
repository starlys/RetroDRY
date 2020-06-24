using System;

namespace RetroDRY
{
    public class Viewon : Daton
    {
        /// <summary>
        /// True if contents were completely loaded; false if the pageNo (see ViewonKey) is not the last page
        /// </summary>
        public bool IsCompleteLoad { get; set; } = true;

        public override Daton Clone(DatonDef datondef)
        {
            var c = base.Clone(datondef) as Viewon;
            c.IsCompleteLoad = IsCompleteLoad;
            return c;
        }
    }
}
