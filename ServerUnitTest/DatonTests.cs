using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class DatonTests
    {
        [TestMethod]
        public void Clone()
        {
            var emily = new Ogre()
            {
                Name = "Emily",
                Key = new PersistonKey("Ogre", "3", false),
                Money = 2,
                OgreId = 3,
                PaymentMethod = new List<Ogre.PaymentMethodRow>
                {
                    new Ogre.PaymentMethodRow { Method = "THUMP", Notes = "taking stuff without money" }
                }
            };

            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Ogre));
            var ogredef = ddict.DatonDefs["Ogre"];

            var clone = emily.Clone(ogredef) as Ogre;
            Assert.IsNotNull(clone);    
            Assert.AreEqual("Emily", clone.Name);
            Assert.AreEqual(1, clone.PaymentMethod.Count);
            Assert.AreEqual("THUMP", clone.PaymentMethod[0].Method);
        }

        [TestMethod]
        public void ComputedCol()
        {
            var thor = new Ogre()
            {
                Name = "Thor",
                Money = 2,
                PaymentMethod = new List<Ogre.PaymentMethodRow>
                {
                    new Ogre.PaymentMethodRow { Method = "credit" }
                }
            };

            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Ogre));
            var ogredef = ddict.DatonDefs["Ogre"];
            thor.Recompute(ogredef);

            Assert.AreEqual("2 gold pieces", thor.FormattedMoney);
            Assert.AreEqual("Arrg, I'll pay with credit", thor.PaymentMethod[0].AngryMethod);
        }
    }
}
