using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
    [TestClass]
    public class CacheTests
    {
        public void CacheCleansBasic()
        {
            var cache = new DatonCache();
            var clients = new ClientPlex();
            for (int i = 0; i < 10; i++) cache.Put(new Ogre() { Key = DatonKey.Parse("Ogre|=" + i) });
            System.Threading.Thread.Sleep(2000);
            for (int i = 10; i < 20; i++) cache.Put(new Ogre() { Key = DatonKey.Parse("Ogre|=" + i) });
            cache.Clean(clients, secondsOld: 1);
            Assert.AreEqual(10, cache.Count);
        }

        [TestMethod]
        public void CacheCleansWithSubscription()
        {
            var cache = new DatonCache();
            var clients = new ClientPlex();
            var user = new User();

            //add datons to cache: 1,2,3
            for (int i = 1; i <= 3; i++) cache.Put(new Ogre() { Key = DatonKey.Parse("Ogre|=" + i) });

            //subscribe to datons 2,3
            string sessionKey = clients.CreateSession(user);
            clients.ManageSubscribe(sessionKey, DatonKey.Parse("Ogre|=2"), "1", true);
            clients.ManageSubscribe(sessionKey, DatonKey.Parse("Ogre|=3"), "1", true);

            //clean cache and there should still be all 3 datons
            cache.Clean(clients, secondsOld: 1);
            Assert.AreEqual(3, cache.Count);

            //wait, clean old, unsubscribe #2, and #3 should still be there
            clients.ManageSubscribe(sessionKey, DatonKey.Parse("Ogre|=2"), "1", false);
            System.Threading.Thread.Sleep(2000);
            cache.Clean(clients, secondsOld: 1);
            Assert.AreEqual(1, cache.Count);

            //unsubscribe #3, clean, and there should be none there
            clients.ManageSubscribe(sessionKey, DatonKey.Parse("Ogre|=3"), "1", false);
            cache.Clean(clients, secondsOld: 1);
            Assert.AreEqual(0, cache.Count);
        }
    }
}
