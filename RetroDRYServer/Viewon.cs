using System;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// A read-only daton with optional parameters, used for queries
    /// </summary>
    public class Viewon : Daton
    {
        /// <summary>
        /// True if contents were completely loaded; false if the pageNo (see ViewonKey) is not the last page
        /// </summary>
        public bool IsCompleteLoad { get; set; } = true;

        /// <summary>
        /// Clone
        /// </summary>
        /// <param name="datondef"></param>
        public override Daton Clone(DatonDef datondef)
        {
            var c = base.Clone(datondef) as Viewon;
            c.IsCompleteLoad = IsCompleteLoad;
            return c;
        }

        /// <summary>
        /// Validate a viewon key; called on a temporary instance of the class, so the implementation should not expect any class members should be set. 
        /// Implementations should call fail one or more times to register failures, passing user-readable messages
        /// </summary>
        public virtual Task ValidateCriteria(IUser user, ViewonKey key, Action<string> fail) { return Task.CompletedTask; }
    }
}
