using System;
using System.Threading.Tasks;
using RetroDRY;

namespace SampleServer.Schema
{
    public static class Initializers
    {
        /// <summary>
        /// Sample validation function (registered in Startup)
        /// </summary>
        public static Task InitializeCustomer(Daton daton)
        {
            if (daton is Customer cust) cust.Notes = "Leads:\r\nComplaints:";
            return Task.CompletedTask;
        }
    }
}
