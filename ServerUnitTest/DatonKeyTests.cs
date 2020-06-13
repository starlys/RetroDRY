using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class DatonKeyTests
    {
        [TestMethod]
        public void All()
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
                new ViewonKey.Criterion("Crazy", "Angry \"O'Leary\" :| :\\") //actual: Angry "O'Leary" :| :\
            };
            k = new ViewonKey("EmpList", criteria);
            Assert.AreEqual(@"EmpList|Crazy=Angry ""O'Leary"" :\| :\\", k.ToString());

        }
    }
}
