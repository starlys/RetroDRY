using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class RetrovertTests
    {
        [TestMethod]
        public void CamelCase()
        {
            Assert.AreEqual("camelCase", Retrovert.CamelCasify("CamelCase"));
            Assert.AreEqual("islower", Retrovert.CamelCasify("islower"));
            Assert.AreEqual("@#", Retrovert.CamelCasify("@#"));
        }

        [TestMethod]
        public void ToCompatibleWire()
        {
            var jill = new Employee
            {
                EmpId = 9,
                FirstName = "Jill",
                Key = new PersistonKey("Employee", "9", false),
                Version = "v2"
            };
            var elist = new EmployeeList
            {
                Key = new ViewonKey("EmployeeList"),
                Version = "v2",
                Employee = new List<EmployeeList.TopRow>
                {
                    new EmployeeList.TopRow
                    {
                        EmpId = 9,
                        FirstName = "Jill"
                        //last name missing
                    }
                }
            };
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Employee));
            ddict.AddDatonUsingClassAnnotation(typeof(EmployeeList));

            string json = Retrovert.ToWire(ddict, jill, true);
            //expected has single quotes so the source code is more readable
            string expected = "{'key':'Employee|=9','version':'v2','employee':{'empId':9,'firstName':'Jill'}}";
            Assert.AreEqual(expected.Replace('\'', '"'), json);

            json = Retrovert.ToWire(ddict, elist, true);
            //expected has single quotes so the source code is more readable
            expected = "{'key':'EmployeeList','version':'v2','employee':[{'empId':9,'firstName':'Jill','lastName':null,'supervisorId':0,'supervisorLastName':null}]}";
            Assert.AreEqual(expected.Replace('\'', '"'), json);
        }

        [TestMethod]
        public void ToDenseWire()
        {
            var jill = new Employee
            {
                EmpId = 9,
                FirstName = "Jill",
                Key = new PersistonKey("Employee", "9", false),
                Version = "v2"
            };
            var elist = new EmployeeList
            {
                Key = new ViewonKey("EmployeeList"),
                Version = "v2",
                Employee = new List<EmployeeList.TopRow>
                {
                    new EmployeeList.TopRow
                    {
                        EmpId = 9,
                        FirstName = "Jill"
                        //last name missing
                    }
                }
            };
            var emily = new Ogre()
            {
                Name = "Emily",
                Key = new PersistonKey("Ogre", "3", false),
                Version = "v2",
                Money = 2,
                OgreId = 3,
                PaymentMethod = new List<Ogre.PaymentMethodRow>
                {
                    new Ogre.PaymentMethodRow { Method = "THUMP", Notes = "n1" },
                    new Ogre.PaymentMethodRow { Method = "BOP", Notes = null }
                }
            };

            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Employee));
            ddict.AddDatonUsingClassAnnotation(typeof(EmployeeList));
            ddict.AddDatonUsingClassAnnotation(typeof(Ogre));

            string json = Retrovert.ToWire(ddict, jill, false);
            //expected has single quotes so the source code is more readable
            string expected = "{'key':'Employee|=9','version':'v2','content':[[9,'Jill']]}";
            Assert.AreEqual(expected.Replace('\'', '"'), json);

            json = Retrovert.ToWire(ddict, elist, false);
            //expected has single quotes so the source code is more readable
            expected = "{'key':'EmployeeList','version':'v2','content':[[9,'Jill',null,0,null]]}";
            Assert.AreEqual(expected.Replace('\'', '"'), json);

            json = Retrovert.ToWire(ddict, emily, false);
            //expected has single quotes so the source code is more readable
            expected = "{'key':'Ogre|=3','version':'v2','content':[[3,'Emily',2,null,[[0,'THUMP','n1',null],[0,'BOP',null,null]]]]}";
            Assert.AreEqual(expected.Replace('\'', '"'), json);
        }

        [TestMethod]
        public void FromCompatibleWire()
        {
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Employee));
            ddict.AddDatonUsingClassAnnotation(typeof(EmployeeList));
            ddict.AddDatonUsingClassAnnotation(typeof(Ogre));

            string json = "{'key':'Employee|=9','version':'v2','employee':[{'empId':9,'firstName':'Jill'}]}".Replace('\'', '"');
            var jobj = JsonConvert.DeserializeObject<JObject>(json);
            var jill0 = Retrovert.FromCompatibleWireFull(ddict, jobj);
            Assert.IsTrue(jill0 is Employee);
            var jill = jill0 as Employee;
            Assert.AreEqual(new PersistonKey("Employee", "9", false), jill.Key);
            Assert.AreEqual("v2", jill.Version);
            Assert.AreEqual(9, jill.EmpId);
            Assert.AreEqual("Jill", jill.FirstName);

            json = "{'key':'EmployeeList','version':'v2','employee':[{'empId':9,'firstName':'Jill','lastName':null,'supervisorId':0,'supervisorLastName':null}]}".Replace('\'', '"');
            jobj = JsonConvert.DeserializeObject<JObject>(json);
            var elist0 = Retrovert.FromCompatibleWireFull(ddict, jobj);
            Assert.IsTrue(elist0 is EmployeeList);
            var elist = elist0 as EmployeeList;
            Assert.AreEqual(new ViewonKey("EmployeeList"), elist.Key);
            Assert.AreEqual("v2", elist.Version);
            Assert.AreEqual(9, elist.Employee[0].EmpId);
            Assert.AreEqual("Jill", elist.Employee[0].FirstName);
            Assert.IsNull(elist.Employee[0].LastName);
        }

        [TestMethod]
        public void FromDiff()
        {
            var emily = new Ogre()
            {
                Name = "Emily",
                Key = new PersistonKey("Ogre", "3", false),
                Version = "v2",
                Money = 2,
                OgreId = 3,
                PaymentMethod = new List<Ogre.PaymentMethodRow>
                {
                    new Ogre.PaymentMethodRow { PaymentMethodId = 91, Method = "THUMP", Notes = "n1" },
                    new Ogre.PaymentMethodRow { PaymentMethodId = 92, Method = "BOP", Notes = null }
                }
            };

            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Ogre));

            string json = "{'key':'Ogre|=9','version':'v2','ogre':[{'ogreId':9,'money':2.50,'paymentMethod-deleted':[{'paymentMethodId':92}],'paymentMethod-new':[{'method':'STOMP','notes':'n2'}]}]}".Replace('\'', '"');
            var jobj = JsonConvert.DeserializeObject<JObject>(json);
            var emilyChanges = Retrovert.FromDiff(ddict, jobj);
            Assert.AreEqual(new PersistonKey("Ogre", "9", false), emilyChanges.Key);
            Assert.AreEqual("v2", emilyChanges.BasedOnVersion);
            Assert.AreEqual(9, emilyChanges.MainTable[0].Columns["OgreId"]);
            Assert.AreEqual((decimal)2.5, emilyChanges.MainTable[0].Columns["Money"]);

            emilyChanges.ApplyTo(ddict.DatonDefs["Ogre"], emily);
            Assert.AreEqual((decimal)2.5, emily.Money);
            Assert.IsFalse(emily.PaymentMethod.Any(m => m.Method == "BOP")); //deleted BOP
            Assert.IsTrue(emily.PaymentMethod.Any(m => m.Method == "STOMP")); //added STOMP
            Assert.IsTrue(emily.PaymentMethod.Any(m => m.Method == "THUMP")); //no change
        }

        [TestMethod]
        public void FormatValues()
        {
            var c = new ColDef { WireType = Constants.TYPE_DATE };
            Assert.AreEqual("null", Retrovert.FormatRawJsonValue(c, null));
            Assert.AreEqual("true", Retrovert.FormatRawJsonValue(c, true));
            Assert.AreEqual("false", Retrovert.FormatRawJsonValue(c, false));
            Assert.AreEqual("254", Retrovert.FormatRawJsonValue(c, (byte)254));
            Assert.AreEqual("987654321", Retrovert.FormatRawJsonValue(c, (long)987654321));
            Assert.AreEqual("987654321.9", Retrovert.FormatRawJsonValue(c, (decimal)987654321.9));
            Assert.AreEqual("\"Jasmine\"", Retrovert.FormatRawJsonValue(c, "Jasmine"));
            Assert.AreEqual("\"The \\\"IT\\\" Crowd\"", Retrovert.FormatRawJsonValue(c, "The \"IT\" Crowd"));
            Assert.AreEqual("\"19991231\"", Retrovert.FormatRawJsonValue(c, new DateTime(1999, 12, 31)));
            c.WireType = Constants.TYPE_DATETIME;
            Assert.AreEqual("\"199912312359\"", Retrovert.FormatRawJsonValue(c, new DateTime(1999, 12, 31, 23, 59, 59, DateTimeKind.Utc)));
        }

        [TestMethod]
        public void Multilanguage()
        {
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Employee));
            var datondef = ddict.DatonDefs["Employee"];
            var firstnamedef = datondef.MainTableDef.FindCol("FirstName");
            firstnamedef.SetPrompt("de", "ERSTE");
            firstnamedef.SetPrompt("fr", "PREMIER");
            Assert.AreEqual(3, firstnamedef.Prompt.Count);
            var birgitte = new User { LangCode = "de" };
            var wiredict = Retrovert.DataDictionaryToWire(ddict, birgitte, null);
            Assert.AreEqual("ERSTE", wiredict.DatonDefs[0].MainTableDef.Cols.Single(c => c.Name == "firstName").Prompt);
            var jaques = new User { LangCode = "fr" };
            wiredict = Retrovert.DataDictionaryToWire(ddict, jaques, null);
            Assert.AreEqual("PREMIER", wiredict.DatonDefs[0].MainTableDef.Cols.Single(c => c.Name == "firstName").Prompt);
        }
    }
}
