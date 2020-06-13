using System;
using RetroDRY;

namespace UnitTest
{
    class User : IUser
    {
        public string Id => "U1";

        public RetroRole[] Roles { get; set; } = new[]
        {
            new RetroRole { BaseLevel = PermissionLevel.All }
        };

        public string LangCode { get; set; } = null;
    }
}
