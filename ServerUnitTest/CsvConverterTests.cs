using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class CsvConverterTests
    {
        [TestMethod]
        public async Task ToCsv()
        {
            //set up exportable data
            var elist = new EmployeeList
            {
                Key = new ViewonKey("EmployeeList"),
                Version = "v2",
                Employee = new List<EmployeeList.TopRow>
                {
                    new EmployeeList.TopRow
                    {
                        EmpId = 9,
                        FirstName = "Jill \"The Shill\" O'Leary",
                        LastName = "multiline\r\nlast name!"
                    }
                }
            };
            var ddict = new DataDictionary();
            ddict.AddDatonUsingClassAnnotation(typeof(EmployeeList));

            //export it to a string
            var lookupResolver = new LookupResolver(datonKey =>
            {
                throw new Exception("not using lookup resolver for this test");
            });
            var converter = new CsvConverter(ddict, lookupResolver, ddict.FindDef(elist));
            var wri1 = new MemoryStream();
            var wri2 = new StreamWriter(wri1);
            await converter.WriteHeaderRow(wri2);
            await converter.WriteAllRows(elist, wri2);
            string csv = System.Text.Encoding.UTF8.GetString(wri1.ToArray());

            //check contents
            string expected = "_RowNumber,EmpId,FirstName,LastName,SupervisorId,SupervisorLastName\r\n1,9,\"Jill \"\"The Shill\"\" O'Leary\",\"multiline\r\nlast name!\",0,\r\n";
            Assert.AreEqual(expected, csv);
        }
    }
}