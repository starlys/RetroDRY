using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class ValidatorTests
    {
        [TestMethod]
        public async Task Validation()
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
            await validator.Validate(datondef, cust);
            Assert.AreEqual(1, validator.Errors.Count); //money out of range

            cust.Money = (decimal)1.5;
            await validator.Validate(datondef, cust);
            Assert.AreEqual(0, validator.Errors.Count); //money fixed

            datondef.CustomValidator = ValidateCustomer;
            await validator.Validate(datondef, cust);
            Assert.AreEqual(1, validator.Errors.Count); //company name bad
        }

        private Task<IEnumerable<string>> ValidateCustomer(Daton cust0)
        {
            var errs = new List<string>();
            var cust = cust0 as Customer;
            if (cust.Company != null && cust.Company.StartsWith("THE")) errs.Add("THE");
            return Task.FromResult(errs as IEnumerable<string>);
        }
    }
}
