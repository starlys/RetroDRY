using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class DatonKeyTests
    {
        [TestMethod]
        public void FormatAsString()
        {
            DatonKey k = new PersistonKey("Emp", "3", false);
            Assert.AreEqual("Emp|=3", k.ToString());

            k = new PersistonKey("Emp", "3", true);
            Assert.AreEqual("Emp|+", k.ToString());

            var criteria = new[]
            {
                new ViewonKey.Criterion("Region", "NE"),
                new ViewonKey.Criterion("Funny", "1")
            };
            k = new ViewonKey("EmpList", criteria, sortColumnName: "LastName", pageNo: 2);
            Assert.AreEqual("EmpList|_page=2|_sort=LastName|Funny=1|Region=NE", k.ToString());

            //now with crazy characters 
            criteria = new[]
            {
                new ViewonKey.Criterion("Crazy", "Angry \"O'Leary\" :| :\\"), //actual: Angry "O'Leary" :| :\
                new ViewonKey.Criterion("Zany", "a=|b|"),
                new ViewonKey.Criterion("Loopy", "\\/") //actual: \/
            };
            k = new ViewonKey("EmpList", criteria);
            Assert.AreEqual(@"EmpList|Crazy=Angry ""O'Leary"" :\| :\\|Loopy=\\/|Zany=a=\|b\|", k.ToString());
        }

        [TestMethod]
        public void Parse()
        { 
            //existing persiston
            var pk = DatonKey.Parse("Emp|=3") as PersistonKey;
            Assert.IsNotNull(pk);
            Assert.AreEqual("Emp", pk.Name);
            Assert.AreEqual("3", pk.PrimaryKey);
            Assert.IsFalse(pk.WholeTable);
            Assert.IsFalse(pk.IsNew);

            //new persiston
            pk = DatonKey.Parse("Emp|=-1") as PersistonKey;
            Assert.IsNotNull(pk);
            Assert.AreEqual("Emp", pk.Name);
            Assert.AreEqual("-1", pk.PrimaryKey);
            Assert.IsFalse(pk.WholeTable);
            Assert.IsTrue(pk.IsNew);

            //whole table persiston
            pk = DatonKey.Parse("EmpType|+") as PersistonKey;
            Assert.IsNotNull(pk);
            Assert.AreEqual("EmpType", pk.Name);
            Assert.IsTrue(pk.WholeTable);
            Assert.IsFalse(pk.IsNew);

            //viewon without criteria
            var vk = DatonKey.Parse("EmpList") as ViewonKey;
            Assert.IsNotNull(vk?.Criteria);
            Assert.AreEqual("EmpList", vk.Name);
            Assert.AreEqual(0, vk.PageNumber);
            Assert.IsNull(vk.SortColumnName);
            Assert.AreEqual(0, vk.Criteria.Count());

            //viewon with escaped criteria
            vk = DatonKey.Parse(@"CustomerList|code1=\\/|code2=\||code3=a=\|b\|") as ViewonKey;
            Assert.IsNotNull(vk?.Criteria);
            Assert.AreEqual("CustomerList", vk.Name);
            Assert.AreEqual(3, vk.Criteria.Count());
            var criArray = vk.Criteria.ToArray();
            Assert.AreEqual("code1", criArray[0].Name);
            Assert.AreEqual(@"\/", criArray[0].PackedValue);
            Assert.AreEqual("code2", criArray[1].Name);
            Assert.AreEqual("|", criArray[1].PackedValue);
            Assert.AreEqual("code3", criArray[2].Name);
            Assert.AreEqual("a=|b|", criArray[2].PackedValue);

            //with sort and page
            vk = DatonKey.Parse("EmpList|_page=2|_sort=firstName") as ViewonKey;
            Assert.IsNotNull(vk?.Criteria);
            Assert.AreEqual("EmpList", vk.Name);
            Assert.IsNotNull(vk);
            Assert.AreEqual(2, vk.PageNumber);
            Assert.AreEqual("firstName", vk.SortColumnName);
            Assert.AreEqual(0, vk.Criteria.Count());
        }
    }
}
