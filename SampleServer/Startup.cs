using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RetroDRY;
using SampleServer.Schema;

namespace SampleServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson(); 
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        public static void InitializeRetroDRY()
        {
            //load configuration (for this sample, we are using a separate file for retrodry features)
            var configBuilder = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings_dev.json");
            var config = configBuilder.Build();
            var dbConnection = config["Database"];

            DbConnection dbResolver(int databaseNumber)
            {
                return new Npgsql.NpgsqlConnection(dbConnection);
            }

            //set up data dictionary from annotations
            var ddict = new DataDictionary();
            ddict.AddDatonsUsingClassAnnotation(typeof(Startup).Assembly);

            //sample to override data dictionary. In a real app the overrides might come from setup tables or be hardcoded, and this process
            //could be moved to a separate class
            //ddict.DatonDefs["Cutomer"] = new DatonDef
            //{
            //  ...
            //};

            //sample custom validation
            ddict.DatonDefs["Cutomer"].CustomValidator = Validators.ValidateCustomer;

            //start up RetroDRY
            ddict.FinalizeInheritance();
            Globals.Retroverse = new Retroverse(SqlFlavorizer.VendorKind.PostgreSQL, ddict, dbResolver);
        }
    }
}
