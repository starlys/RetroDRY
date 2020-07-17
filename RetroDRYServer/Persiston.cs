using System;
using System.Threading.Tasks;

namespace RetroDRY
{
    public class Persiston : Daton
    {
        /// <summary>
        /// Validate this persiston; called before saving. Implementations should call fail one or more times to register failures, passing 
        /// user-readable messages
        /// </summary>
        public virtual Task Validate(IUser user, Action<string> fail) { return Task.CompletedTask; }
    }
}
