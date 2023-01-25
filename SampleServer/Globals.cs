using System;
using RetroDRY;

#pragma warning disable CA2211 // Non-constant fields should not be visible

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
