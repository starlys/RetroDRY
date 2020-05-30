using System;
using System.Collections.Generic;
using System.Text;

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
        }

        private readonly ClientPlex ClientPlex;

        public Diagnostics(ClientPlex clientplex)
        {
            ClientPlex = clientplex;
        }

        public Report GetStatus()
        {
            return new Report
            {
                NumSessions = ClientPlex.SessionCount
            };
        }

        /// <summary>
        /// When set by the host app, is then called whenever an internal error occurs during a client request
        /// </summary>
        public Action<string> ReportClientCallError;
    }
}
