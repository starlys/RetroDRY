using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class SqlWriteBuilderTests
    {
        [TestMethod]
        public void Insert()
        {
            var db = new FakeDbConnection();
            var builder = new SqlInsertBuilder(new SqlFlavorizer(SqlFlavorizer.VendorKind.PostgreSQL), s => s + " (customized)");
            builder.AddNonKey("FirstName", "string", "Jane");
            builder.AddNonKey("MiddleName", "nstring", null);
            builder.AddNonKey("LastName", "string", null);
            var pk = builder.Execute(db, "Emp", "EmpId", true).Result;

            Assert.AreEqual("Jane", db.TheCommand.TheParameters[0].Value);
            Assert.IsNull(db.TheCommand.TheParameters[1].Value);
            Assert.AreEqual("", db.TheCommand.TheParameters[2].Value);

            Assert.AreEqual("insert into Emp (FirstName,MiddleName,LastName) values (@p0,@p1,@p2) returning EmpId (customized)", db.TheCommand.CommandText);
        }

        [TestMethod]
        public void Update()
        {
            var db = new FakeDbConnection();
            var builder = new SqlUpdateBuilder(new SqlFlavorizer(SqlFlavorizer.VendorKind.PostgreSQL), s => s + " (customized)");
            builder.AddNonKey("FirstName", "string", "Jane");
            builder.AddNonKey("MiddleName", "nstring", null);
            builder.AddNonKey("LastName", "string", null);
            builder.Execute(db, "Emp", "EmpId", true).Wait();

            Assert.AreEqual("update Emp set FirstName=@p0,MiddleName=@p1,LastName=@p2 where EmpId=@pk (customized)", db.TheCommand.CommandText);
        }
    }
}
