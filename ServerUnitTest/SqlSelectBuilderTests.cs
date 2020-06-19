using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class SqlSelectBuilderTests
    {
        [TestMethod]
        public void All()
        {
            var returnColumnNames = new List<string> { "f1", "f2" };
            var builder = new SqlSelectBuilder(new SqlFlavorizer(SqlFlavorizer.VendorKind.PostgreSQL), "Emp", "LastName", returnColumnNames);
            builder.WhereClause.AddWhere("IsActive<>0");
            builder.WhereClause.AddWhere("State=" + builder.WhereClause.NextParameterName(), "Ohio");
            Assert.AreEqual("@p1", builder.WhereClause.NextParameterName());
            var cmd = new NpgsqlCommand();
            builder.WhereClause.ExportParameters(cmd);
            Assert.AreEqual("Ohio", cmd.Parameters[0].Value);
            Assert.AreEqual("select f1,f2 from Emp where IsActive<>0 and State=@p0 order by Emp.LastName", builder.ToString());

            //with paging
            builder.PageSize = 100;
            builder.PageNo = 1;
            Assert.AreEqual("select f1,f2 from Emp where IsActive<>0 and State=@p0 order by Emp.LastName limit 101 offset 100", builder.ToString());
        }

        [TestMethod]
        public void FormatInClause()
        {
            string inclause = SqlSelectBuilder.FormatInClauseList(new object[] { 1, "2", "three", 4.0 });
            Assert.AreEqual("1,'2','three',4", inclause);
        }
    }
}
