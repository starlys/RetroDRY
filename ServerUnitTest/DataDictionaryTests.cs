using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class DataDictionaryTests
    {
        [TestMethod]
        public void PersistonFromAnnotations()
        {
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Customer));
            var customerdef = ddict.DatonDefs["Customer"];
            Assert.AreEqual(typeof(Customer), customerdef.Type);
            Assert.AreEqual(typeof(Customer), customerdef.MainTableDef!.RowType);
            Assert.AreEqual(6, customerdef.DatabaseNumber);
            Assert.AreEqual("CustomerId", customerdef.MainTableDef.PrimaryKeyColName);
            Assert.IsNull(customerdef.MainTableDef.Children);
            Assert.AreEqual(5, customerdef.MainTableDef.Cols.Count);
        }

        [TestMethod]
        public void MultiRowPersistonFromAnnotations()
        {
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(ExtCustomer));
            var customerdef = ddict.DatonDefs["ExtCustomer"];
            Assert.AreEqual(typeof(ExtCustomer), customerdef.Type);
            Assert.AreEqual(typeof(ExtCustomer.ExtRow), customerdef.MainTableDef!.RowType);
            Assert.IsNull(customerdef.MainTableDef.Children);
            Assert.AreEqual(1, customerdef.MainTableDef.Cols.Count);
        }

        [TestMethod]
        public void ViewonFromAnnotations()
        {
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(EmployeeList));
            var viewondef = ddict.DatonDefs["EmployeeList"];
            Assert.IsNotNull(viewondef?.CriteriaDef);
            Assert.AreEqual(1, viewondef.CriteriaDef.Cols.Count);
            Assert.AreEqual("LastName", viewondef.CriteriaDef.Cols[0].Name);
        }

        [TestMethod]
        public void Inheritance()
        {
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(Employee));
            ddict.AddDatonUsingClassAnnotation(typeof(EmployeeList));
            ddict.FinalizeInheritance();
            var viewondef = ddict.DatonDefs["EmployeeList"];
            var firstnamedef = viewondef.MainTableDef!.Cols.FirstOrDefault(c => c.Name == "FirstName");
            Assert.IsNotNull(firstnamedef);
            Assert.AreEqual(50, firstnamedef.MaxLength); //this is inherited
            Assert.AreEqual(0, firstnamedef.MinLength); //this is not inherited
        }
    }
}
