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
            var cust = daton as Customer;
            cust.Notes = "Leads:\r\nComplaints:";
            return Task.CompletedTask;
        }
    }
}
