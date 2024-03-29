﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

#pragma warning disable IDE0017

namespace UnitTest
{
    [TestClass]
    public class SecurityGuardTests
    {
        [TestMethod]
        public void View()
        {
            var xorg = new Ogre
            {
                Key = new PersistonKey("Ogre", DatonKey.NEWPK, false),
                Name = "Xorg",
                Money = 4,
                PaymentMethod = new List<Ogre.PaymentMethodRow>
                {
                    new Ogre.PaymentMethodRow { Method = "credit", Notes = "usually declined"}
                }
            };
            
            //bill can't view money
            var bill = new User();
            bill.Roles = new[]
            {
                new RetroRole 
                {
                    BaseLevel = PermissionLevel.View ,
                    TableOverrides = new List<TablePermission>
                    {
                        new TablePermission("Ogre", PermissionLevel.View | PermissionLevel.Modify)
                        {
                            ColumnOverrides = new List<ColumnPermission>
                            {
                                new ColumnPermission("Money", PermissionLevel.None)
                            }
                        },
                        new TablePermission("PaymentMethod", PermissionLevel.None)
                        {
                            Level = (usr, daton, tabname) => PermissionLevel.None
                        }
                    }
                }
            };

            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Ogre));
            var guard = new SecurityGuard(ddict, bill);
            guard.HidePrivateParts(xorg);

            Assert.IsNotNull(xorg.Name);
            Assert.IsNull(xorg.Money);
            Assert.AreEqual(1, xorg.PaymentMethod.Count);
            Assert.IsNull(xorg.PaymentMethod[0].Method);
            Assert.IsNull(xorg.PaymentMethod[0].Notes);
        }

        [TestMethod]
        public void Update()
        {
            var xorg = new Ogre
            {
                Key = new PersistonKey("Ogre", DatonKey.NEWPK, false),
                Name = "Xorg",
                Money = 4,
                PaymentMethod = new List<Ogre.PaymentMethodRow>
                {
                    new Ogre.PaymentMethodRow { Method = "credit", Notes = "usually declined"}
                }
            };

            //bill can't update money
            var bill = new User();
            bill.Roles = new[]
            {
                new RetroRole
                {
                    BaseLevel = PermissionLevel.All,
                    TableOverrides = new List<TablePermission>
                    {
                        new TablePermission("Ogre", PermissionLevel.View | PermissionLevel.Modify)
                        {
                            ColumnOverrides = new List<ColumnPermission>
                            {
                                new ColumnPermission("Money",  PermissionLevel.None)
                            }
                        },
                        new TablePermission("PaymentMethod", PermissionLevel.None)
                        {
                            Level = (usr, daton, tabname) => PermissionLevel.None
                        }
                    }
                }
            };

            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Ogre));
            var ogredef = ddict.DatonDefs["Ogre"];
            var paymentdef = ogredef.MainTableDef!.Children![0];

            var diff = new PersistonDiff(ogredef, xorg.Key, xorg.Version)
            {
                MainTable = new List<PersistonDiff.DiffRow>
                {
                    new PersistonDiff.DiffRow
                    {
                        Kind = DiffKind.Other,
                        Columns = new Dictionary<string, object?>
                        {
                            { "Name", "Priscilla" }, //allowed
                            { "Money", (decimal)5.49 } //disallowed
                        },
                        ChildTables = new Dictionary<TableDef, List<PersistonDiff.DiffRow>>
                        {
                            {
                                paymentdef,
                                new List<PersistonDiff.DiffRow>
                                {
                                    new PersistonDiff.DiffRow
                                    {
                                        Kind = DiffKind.Other,
                                        Columns = new Dictionary<string, object?>
                                        {
                                            { "Method", "cash" }, //disallowed by function
                                            { "Notes", "cash is best" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var guard = new SecurityGuard(ddict, bill);
            var errors = guard.GetDisallowedWrites(xorg, ogredef, diff).ToArray();

            Assert.AreEqual(3, errors.Length);
            Assert.IsTrue(errors[0].Contains("Ogre.Money"));
            Assert.IsTrue(errors[1].Contains("PaymentMethod.Method"));
            Assert.IsTrue(errors[2].Contains("PaymentMethod.Notes"));
        }

        [TestMethod]
        public void Create()
        {
            var xorg = new Ogre
            {
                Key = new PersistonKey("Ogre", DatonKey.NEWPK, false),
                Name = "Xorg",
                Money = 4,
                PaymentMethod = new List<Ogre.PaymentMethodRow>
                {
                    new Ogre.PaymentMethodRow { Method = "credit", Notes = "usually declined"}
                }
            };

            //nancy can only create new payment rows, not update existing
            var nancy = new User();
            nancy.Roles = new[]
            {
                new RetroRole
                {
                    BaseLevel = PermissionLevel.None,
                    TableOverrides = new List<TablePermission>
                    {
                        new TablePermission("PaymentMethod",PermissionLevel.Create)
                    }
                }
            };

            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Ogre));
            var ogredef = ddict.DatonDefs["Ogre"];
            var paymentdef = ogredef.MainTableDef!.Children![0];

            var diff = new PersistonDiff(ogredef, xorg.Key, xorg.Version)
            {
                MainTable = new List<PersistonDiff.DiffRow>
                {
                    new PersistonDiff.DiffRow
                    {
                        Kind = DiffKind.Other,
                        Columns = new Dictionary<string, object?>
                        {
                            { "Name", "Priscilla" } //disallowed
                        },
                        ChildTables = new Dictionary<TableDef, List<PersistonDiff.DiffRow>>
                        {
                            {
                                paymentdef,
                                new List<PersistonDiff.DiffRow>
                                {
                                    new PersistonDiff.DiffRow
                                    {
                                        Kind = DiffKind.Other,
                                        Columns = new Dictionary<string, object?>
                                        {
                                            { "Notes", "disallowed" } //disallowed update
                                        }
                                    },
                                    new PersistonDiff.DiffRow
                                    {
                                        Kind = DiffKind.NewRow,
                                        Columns = new Dictionary<string, object?>
                                        {
                                            { "Method", "barter" } //allowed create row
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var guard = new SecurityGuard(ddict, nancy);
            var errors = guard.GetDisallowedWrites(xorg, ogredef, diff).ToArray();

            Assert.AreEqual(2, errors.Length);
            Assert.IsTrue(errors[0].Contains("Ogre.Name"));
            Assert.IsTrue(errors[1].Contains("PaymentMethod.Notes"));
        }
    }
}
