using System;
using System.Collections.Generic;
using RetroDRY;

#pragma warning disable CA2211 // Non-constant fields should not be visible

namespace SampleServer
{
    /// <summary>
    /// Hardcoded cache of users for a sample app. A real app would store users and roles in a databsae and cache information on recent
    /// requests
    /// </summary>
    public static class UserCache
    {
        private static readonly RetroRole AdminRole = new()
        {
            BaseLevel = PermissionLevel.All
        };
        private static readonly RetroRole SalesRole = new()
        {
            BaseLevel = PermissionLevel.View,
            TableOverrides = new List<TablePermission>
            {
                new TablePermission("Customer", PermissionLevel.All),
                new TablePermission("Sale", PermissionLevel.All),
                new TablePermission("SaleItem", PermissionLevel.All),
                new TablePermission("SaleItemNote", PermissionLevel.All)
            }
        };
        private static readonly RetroRole PublicRole = new()
        {
            BaseLevel = PermissionLevel.None, 
            TableOverrides = new List<TablePermission>
            {
                new TablePermission("Item", PermissionLevel.View),
                new TablePermission("ItemVariant", PermissionLevel.View),
                new TablePermission("PhoneType", PermissionLevel.View),
                new TablePermission("SaleStatus", PermissionLevel.View),
                new TablePermission("Sale", PermissionLevel.Create), 
                new TablePermission("SaleItem", PermissionLevel.Create)
            }
        };
        private static readonly RetroRole CustomerNotesRole = new()
        {
            BaseLevel = PermissionLevel.None,
            TableOverrides = new List<TablePermission>
            {
                new TablePermission("Customer", PermissionLevel.Modify)
                {
                    TableName = "", 
                    BaseLevel = PermissionLevel.Modify,
                    ColumnOverrides = new List<ColumnPermission>
                    {
                        new ColumnPermission("CustomerId", PermissionLevel.View),
                        new ColumnPermission("Company", PermissionLevel.View),
                        //must be invisible for integration tests: new ColumnPermission { ColumnName = "SalesRepId", BaseLevel = PermissionLevel.View },
                        new ColumnPermission("Notes", PermissionLevel.View | PermissionLevel.Modify)
                    }
                }
            }
        };

        public class User : IUser
        {
            public string Id { get; set; }
            public string Password { get; set; }
            public RetroRole[] Roles { get; set; } = Array.Empty<RetroRole>();
            public string? LangCode { get; set; }
            
            /// <summary>
            /// A real app would store users' timezones and calculate the minutes offset from UTC
            /// </summary>
            public int TimeOffsetMinutes;

            public User(string id, string password, RetroRole[] roles, string? langCode)
            {
                Id = id;
                Password = password;
                Roles = roles;
                LangCode = langCode;
            }
        }

        public static User[] Users = new[]
        {
            new User("buffy", "spiffy", new[] { AdminRole }, null) { TimeOffsetMinutes = -4 * 60 },
            new User("buffy", "arfarf", new[] { SalesRole }, null) { TimeOffsetMinutes = -6 * 60 },
            new User ("buffy", "public", new[] { PublicRole }, null) { TimeOffsetMinutes = -4 * 60 },
            new User ("buffy", "steno", new[] { PublicRole, CustomerNotesRole }, null) { TimeOffsetMinutes = -4 * 60 }
        };

        public static User Buffy_The_Admin => Users[0];
        public static User Spot_The_Salesman => Users[1];
        public static User PublicUser => Users[2];
        public static User Nate_The_Noter => Users[3];
    }
}
