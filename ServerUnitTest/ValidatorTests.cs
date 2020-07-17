using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class ValidatorTests
    {
        [TestMethod]
        public async Task ValidatePersiston()
        {
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Customer));
            var datondef = ddict.DatonDefs["Customer"];
            var cust = new Customer()
            {
                Company = "THE COMPANY",
                Money = -3
            };
            var validator = new Validator(new User());
            await validator.ValidatePersiston(datondef, cust);
            Assert.AreEqual(2, validator.Errors.Count); //company name bad and money out of range

            cust.Money = (decimal)1.5;
            await validator.ValidatePersiston(datondef, cust);
            Assert.AreEqual(1, validator.Errors.Count); //money fixed

            cust.Company = "FIXED COMPANY";
            await validator.ValidatePersiston(datondef, cust);
            Assert.AreEqual(0, validator.Errors.Count); //company name fixed
        }

        [TestMethod]
        public async Task ValidateViewon()
        {
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(EmployeeList));
            var datondef = ddict.DatonDefs["EmployeeList"];
            var validator = new Validator(new User());

            var viewonKey = new ViewonKey("EmployeeList", new[] { new ViewonKey.Criterion("LastName", "Wash") });
            await validator.ValidateCriteria(datondef, viewonKey);
            Assert.AreEqual(0, validator.Errors.Count); //last name is ok

            viewonKey = new ViewonKey("EmployeeList", new[] { new ViewonKey.Criterion("LastName", "Washington") });
            await validator.ValidateCriteria(datondef, viewonKey);
            Assert.AreEqual(1, validator.Errors.Count); //last name criteria is too long

            viewonKey = new ViewonKey("EmployeeList", new[] { new ViewonKey.Criterion("LastName", "THEM") });
            await validator.ValidateCriteria(datondef, viewonKey);
            Assert.AreEqual(1, validator.Errors.Count); //last name is not valid by custom rule
        }

    }
}
