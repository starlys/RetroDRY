using System;
using RetroDRY;

namespace UnitTest
{
    class User : IUser
    {
        public virtual string Id => "U1";

        public RetroRole[] Roles { get; set; } = new[]
        {
            new RetroRole { BaseLevel = PermissionLevel.All }
        };

        public string? LangCode { get; set; } = null;
    }

    class SpecificUser : User
    {
        readonly string _id;
        public SpecificUser(string id) { _id = id; }
        public override string Id => _id;
    }
}
