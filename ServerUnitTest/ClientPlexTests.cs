using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class ClientPlexTests
    {
        [TestMethod]
        public void CleansOldClients()
        {
            //var cache = new DatonCache();
            var clients = new ClientPlex();
            var user1 = new SpecificUser("user1");
            var user2 = new SpecificUser("user2");
            var user3 = new SpecificUser("user3");

            //connect 3 users
            Assert.AreEqual(0, clients.SessionCount);
            string sessionKey1 = clients.CreateSession(user1);
            string sessionKey2 = clients.CreateSession(user2);
            string sessionKey3 = clients.CreateSession(user3);
            Assert.AreEqual(3, clients.SessionCount);

            //disconnect user 1 intentionally
            clients.DeleteSession(sessionKey1);
            Assert.AreEqual(2, clients.SessionCount);

            //time out user 2, while user 3 is in progress
            System.Threading.Thread.Sleep(1500);
            clients.GetUser(sessionKey3);
            clients.Clean(null, secondsOld: 1).Wait();
            Assert.AreEqual(1, clients.SessionCount);

            //time out user 3
            System.Threading.Thread.Sleep(1500);
            clients.Clean(null, secondsOld: 1).Wait();
            Assert.AreEqual(0, clients.SessionCount);
        }
    }
}
