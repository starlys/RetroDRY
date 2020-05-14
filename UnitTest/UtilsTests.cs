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
            Assert.AreEqual("bool", RetroDRY.Utils.InferredWireType(typeof(bool)));
            Assert.AreEqual("nbool", RetroDRY.Utils.InferredWireType(typeof(bool?)));
            Assert.AreEqual("blob", RetroDRY.Utils.InferredWireType(typeof(byte[])));
            Assert.ThrowsException<Exception>(() => RetroDRY.Utils.InferredWireType(typeof(Task)));
        }

        [TestMethod]
        public void Construct()
        {
            Assert.IsTrue(RetroDRY.Utils.Construct(typeof(UtilsTests)).GetType() == typeof(UtilsTests));
            Assert.ThrowsException<Exception>(() => RetroDRY.Utils.Construct(typeof(Retroverse))); //no parameterless ctor
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
            var intlist = RetroDRY.Utils.CreateOrGetFieldValue<IList>(foo, field);
            Assert.AreEqual(21, intlist[0]);

            //when field is null
            foo = new Foo();
            intlist = RetroDRY.Utils.CreateOrGetFieldValue<IList>(foo, field);
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
            Assert.AreEqual(1, RetroDRY.Utils.IndexOfPrimaryKeyMatch(list, pkfield, 6));
            Assert.AreEqual(-1, RetroDRY.Utils.IndexOfPrimaryKeyMatch(list, pkfield, 7));
        }
    }

    class Foo
    {
        public string Name;
        public int Id;
        public List<int> IntList;
    }
}
