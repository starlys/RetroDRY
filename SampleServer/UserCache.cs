using Microsoft.AspNetCore.Mvc.Formatters;
using RetroDRY;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SampleServer
{
    /// <summary>
    /// Hardcoded cache of users for a sample app. A real app would store users and roles in a databse and cache information on recent
    /// requests
    /// </summary>
    public static class UserCache
    {
        private static readonly RetroRole AdminRole = new RetroRole
        {
            BaseLevel = PermissionLevel.All
        };
        private static readonly RetroRole SalesRole = new RetroRole
        {
            BaseLevel = PermissionLevel.View,
            TableOverrides = new List<TablePermission>
            {
                new TablePermission { TableName = "Customer", BaseLevel = PermissionLevel.All },
                new TablePermission { TableName = "Sale", BaseLevel = PermissionLevel.All } //child tables SaleItem, SaleItemNote are included in this 
            }
        };
        private static readonly RetroRole PublicRole = new RetroRole
        {
            BaseLevel = PermissionLevel.None,
            TableOverrides = new List<TablePermission>
            {
                new TablePermission { TableName = "Sale", BaseLevel = PermissionLevel.Create } 
            }
        };
        private static readonly RetroRole CustomerNotesRole = new RetroRole
        {
            BaseLevel = PermissionLevel.None,
            TableOverrides = new List<TablePermission>
            {
                new TablePermission 
                {
                    TableName = "Customer", 
                    BaseLevel = PermissionLevel.None,
                    ColumnOverrides = new List<ColumnPermission>
                    {
                        new ColumnPermission { ColumnName = "CustomerId", BaseLevel = PermissionLevel.View },
                        new ColumnPermission { ColumnName = "Notes", BaseLevel = PermissionLevel.View | PermissionLevel.Modify }
                    }
                }
            }
        };

        public class User : IUser
        {
            public string Id { get; set; }
            public string Password { get; set; }
            public RetroRole[] Roles { get; set; }
            public string LangCode { get; set; }
            
            /// <summary>
            /// A real app would store users' timezones and calculate the minutes offset from UTC
            /// </summary>
            public int TimeOffsetMinutes;
        }

        public static User[] Users = new[]
        {
            new User { Id = "buffy", Password = "spiffy", TimeOffsetMinutes = -4 * 60, Roles = new[] { AdminRole } },
            new User { Id = "spot", Password = "arfarf", TimeOffsetMinutes = -6 * 60, Roles = new[] { SalesRole } },
            new User { Id = "public", Password = "public", TimeOffsetMinutes = -4 * 60, Roles = new[] { PublicRole } },
            new User { Id = "nate", Password = "steno", TimeOffsetMinutes = -4 * 60, Roles = new[] { PublicRole, CustomerNotesRole } }
        };

        public static User Buffy_The_Admin => Users[0];
        public static User Spot_The_Salesman => Users[1];
        public static User PublicUser => Users[2];
        public static User Nate_The_Noter => Users[3];
    }
}
