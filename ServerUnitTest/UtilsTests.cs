using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class UtilsTests
    {
        [TestMethod]
        public void InferredWireType()
        {
            Assert.AreEqual("bool", Utils.InferredWireType(typeof(bool)));
            Assert.AreEqual("nbool", Utils.InferredWireType(typeof(bool?)));
            Assert.AreEqual("blob", Utils.InferredWireType(typeof(byte[])));
            Assert.ThrowsException<Exception>(() => Utils.InferredWireType(typeof(Task)));
        }

        [TestMethod]
        public void Construct()
        {
            Assert.IsTrue(Utils.Construct(typeof(UtilsTests)).GetType() == typeof(UtilsTests));
            Assert.IsTrue(Utils.Construct(typeof(Retroverse)).GetType() == typeof(Retroverse)); 
        }

        [TestMethod]
        public void CreateOrGetFieldValue()
        {
            //when field already has a value
            var foo = new Foo()
            {
                IntList = new List<int>()
            };
            foo.IntList.Add(21);
            var field = foo.GetType().GetField("IntList");
            Assert.IsNotNull(field);
            var intlist = Utils.CreateOrGetFieldValue<IList>(foo, field);
            Assert.IsNotNull(intlist);
            Assert.AreEqual(21, intlist[0]);

            //when field is null
            foo = new Foo();
            intlist = Utils.CreateOrGetFieldValue<IList>(foo, field);
            Assert.IsNotNull(intlist);
            foo.IntList.Add(56);
            Assert.AreEqual(56, intlist[0]);
        }

        [TestMethod]
        public void IndexOfPrimaryKeyMatch()
        {
            var list = new List<Foo> 
            {
                new Foo { Id = 5, Name = "Mary" },
                new Foo { Id = 6, Name = "Jane" }
            };
            var pkfield = typeof(Foo).GetField("Id");
            Assert.IsNotNull(pkfield);
            Assert.AreEqual(1, Utils.IndexOfPrimaryKeyMatch(list, pkfield, 6));
            Assert.AreEqual(-1, Utils.IndexOfPrimaryKeyMatch(list, pkfield, 7));
        }
    }

    class Foo
    {
        public string? Name;
        public int Id;
        public List<int> IntList = new();
    }
}
