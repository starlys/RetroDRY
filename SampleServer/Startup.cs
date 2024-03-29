using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RetroDRY;
using SampleServer.Schema;

namespace SampleServer;

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
        services.AddCors();
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

        app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    public static void InitializeRetroDRY(bool integrationTestMode = false)
    {
        //load configuration (for this sample, we are using a separate settings file for retrodry features)
        var configBuilder = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings_dev.json");
        var config = configBuilder.Build();
        var dbConnection = config["Database"] ?? throw new Exception("Database missing in onfig");
        Globals.ConnectionString = dbConnection;

        //This will tell RetroDRY how to access your database
        Task<DbConnection> dbResolver(int databaseNumber)
        {
            var db = new Npgsql.NpgsqlConnection(dbConnection);
            db.Open();
            return Task.FromResult(db as DbConnection);
        }

        //build data dictionary from annotations
        var ddict = new DataDictionary();
        ddict.AddDatonsUsingClassAnnotation(typeof(Startup).Assembly);

        //sample to override data dictionary. In a real app the overrides might come from setup tables or be hardcoded, and this process
        //could be moved to a separate class
        //ddict.DatonDefs["Customer"] = new DatonDef
        //{
        //  ...
        //};
        //ddict.DatonDefs["Customer"].MainTableDef.Prompt["de"] = "..,";

        //sample custom values (dynamic columns that are not declared in the database or at compile time)
        ddict.DatonDefs["Sale"].MainTableDef!.AddCustomColum("CouponCode", typeof(string), Constants.TYPE_NSTRING);
        ddict.DatonDefs["Sale"].MainTableDef!.AddCustomColum("IsRushOrder", typeof(bool?), Constants.TYPE_NBOOL).SetPrompt("", "Is Rush Order");
        ddict.DatonDefs["Sale"].MainTableDef!.AddCustomColum("IsInternalSale", typeof(bool), Constants.TYPE_BOOL);

        //sample default values initializer
        ddict.DatonDefs["Customer"].Initializer = Initializers.InitializeCustomer;

        //start up RetroDRY
        ddict.FinalizeInheritance();
        Globals.Retroverse?.Dispose();
        Globals.Retroverse = new Retroverse
        {
            ViewonPageSize = 50
        };
        Globals.Retroverse.Initialize(SqlFlavorizer.VendorKind.PostgreSQL, ddict, dbResolver, integrationTestMode: integrationTestMode);

        //error reporting; In a real app you would send this to your logging destinations
        if (Globals.Retroverse.Diagnostics != null)
            Globals.Retroverse.Diagnostics.ReportClientCallError = msg => Console.WriteLine(msg);

        //sample SQL overide
        Globals.Retroverse.OverrideSql("Customer", new CustomerSql());
        Globals.Retroverse.OverrideSql("CustomerList", new CustomerListSql());

        //sample exception text rewriter
        Globals.Retroverse.CleanUpSaveException = (user, ex) =>
        {
            if (ex.Message.Contains("violates foreign key constraint")) return "Cannot delete record because other records depend on it.";
            return ex.Message;
        };

        //only for integration testing
        if (integrationTestMode)
            InitializeRetroDRYIntegrationTesting(ddict, dbResolver);
    }

    /// <summary>
    /// Don't include this in a real app; see Program.IntegrationTestingSetup
    /// </summary>
    static void InitializeRetroDRYIntegrationTesting(DataDictionary ddict, Func<int, Task<DbConnection>> dbResolver)
    {
        //set up multiple parallel "servers"
        Globals.Retroverse.ViewonPageSize = 500;
        Globals.TestingRetroverse[0] = Globals.Retroverse;
        Globals.TestingRetroverse[1]?.Dispose();
        Globals.TestingRetroverse[1] = new Retroverse();
        Globals.TestingRetroverse[1].Initialize(SqlFlavorizer.VendorKind.PostgreSQL, ddict, dbResolver, integrationTestMode: true);
        Globals.TestingRetroverse[2]?.Dispose();
        Globals.TestingRetroverse[2] = new Retroverse();
        Globals.TestingRetroverse[2].Initialize(SqlFlavorizer.VendorKind.PostgreSQL, ddict, dbResolver, integrationTestMode: true);

        //ensure BigTable has lots of rows in it
        using var db = Globals.Retroverse!.GetDbConnection!(0).Result;
        using var cmd = db.CreateCommand();
        cmd.CommandText = "select count(*) from BigTable";
        long rowCount = Convert.ToInt64(cmd.ExecuteScalar());
        while (rowCount++ < 500100)
        {
            cmd.CommandText = $"insert into BigTable(name) values('{Guid.NewGuid()}')";
            cmd.ExecuteNonQuery();
        }
    }
}
