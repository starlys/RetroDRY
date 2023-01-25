using System;

namespace RetroDRY
{
    /// <summary>
    /// A user to be defined by host app
    /// </summary>
    public interface IUser
    {
        /// <summary>
        /// The user ID. This should be set to an email address or login credential to identify when different user sessions are the same user; it is only
        /// used by RetroDRY in rare cases when user permissions change during a session.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Collection of roles the user has, which define permissions
        /// </summary>
        RetroRole[] Roles { get; }

        /// <summary>
        /// Null for the default language, or a code that matches the code used when adding prompts
        /// </summary>
        string LangCode { get; }
    }

}
