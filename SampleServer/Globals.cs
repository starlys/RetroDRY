using System;
using RetroDRY;

namespace SampleServer
{
    public static class Globals
    {
        public static string ConnectionString;
        public static Retroverse Retroverse;

        /// <summary>
        /// Retroverse instances for integration testing only (instance 0 is the same as Globals.Retroverse)
        /// </summary>
        public static Retroverse[] TestingRetroverse = new Retroverse[3];
    }
}
