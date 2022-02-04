using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace SampleServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Startup.InitializeRetroDRY();
                CreateHostBuilder(args).Build().Run();
            }
            finally
            {
                Globals.Retroverse?.Dispose();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    IntegrationTestingSetup(webBuilder);
                });


        /// <summary>
        /// Don't include this or the call to this method in a real app; only for integration testing.
        /// Sets up additional ports for the 2nd and 3rd pseudo-server for testing multi-server scenarios
        /// </summary>
        private static void IntegrationTestingSetup(IWebHostBuilder webBuilder)
        {
            webBuilder.UseKestrel(wopts =>
            {
                //note 5001 is default https, used by default when UseKestrel isn't called
                wopts.ListenLocalhost(5001, opts => opts.UseHttps());
                wopts.ListenLocalhost(5002, opts => opts.UseHttps());
                wopts.ListenLocalhost(5003, opts => opts.UseHttps());
            });
        }
    }
}
