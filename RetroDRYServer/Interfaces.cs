using System;

namespace RetroDRY
{
    /// <summary>
    /// A user to be defined by host app
    /// </summary>
    public interface IUser
    {
        string Id { get; }
        RetroRole[] Roles { get; }

        /// <summary>
        /// Null for the default language, or a code that matches the code used when adding prompts
        /// </summary>
        string LangCode { get; }
    }

}
