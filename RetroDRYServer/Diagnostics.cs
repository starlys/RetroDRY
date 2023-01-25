using System;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace RetroDRY
{
    /// <summary>
    /// Behavior to collect and report diagnostics, mainly for debugging and integration testing
    /// </summary>
    public class Diagnostics
    {
        public class Report
        {
            public int NumSessions;
            public int NumCachedDatons;
            public int NumCachedViewons;

            /// <summary>
            /// Number of daton loads from database since last report
            /// </summary>
            public int LoadCount;

            /// <summary>
            /// Number of subscriptions across all sessions
            /// </summary>
            public int SubscriptionCount;
        }

        private readonly ClientPlex ClientPlex;
        private readonly DatonCache DatonCache;
        private int LoadCount;

        public Diagnostics(ClientPlex clientplex, DatonCache datonCache)
        {
            ClientPlex = clientplex;
            DatonCache = datonCache;
        }

        public Report GetStatus()
        {
            try
            {
                return new Report
                {
                    NumSessions = ClientPlex.SessionCount,
                    NumCachedDatons = DatonCache.Count,
                    NumCachedViewons = DatonCache.CountViewons,
                    LoadCount = LoadCount,
                    SubscriptionCount = ClientPlex.SubscriptionCount
                };
            }
            finally
            {
                LoadCount = 0;
            }
        }

        /// <summary>
        /// When set by the host app, is then called whenever an internal error occurs during a client request
        /// </summary>
        public Action<string> ReportClientCallError;

        internal void IncrementLoadCount()
        {
            ++LoadCount;
        }
    }
}
