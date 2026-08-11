using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CuMusicClub.Application.FunctionalTests.Infrastructure;

public class WebApiFactory : WebApplicationFactory<Program>
{
    public const string TestDatabaseName = "CuMusicClubTest";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Precedence: TEST_CONNECTION_STRING env var, else the appsettings connection string
        // with the database swapped to the dedicated test DB (keeps credentials from appsettings),
        // else a sane local default.
        builder.UseSetting("ConnectionStrings:CuMusicClubDb", ResolveTestConnectionString(builder));
    }

    private static string ResolveTestConnectionString(IWebHostBuilder builder)
    {
        var fromEnv = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        var contentRoot = builder.GetSetting(WebHostDefaults.ContentRootKey);
        var environmentName = builder.GetSetting(WebHostDefaults.EnvironmentKey) ?? "Development";

        string? configured = null;
        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            configured = new ConfigurationBuilder().SetBasePath(contentRoot)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
                .Build()
                .GetConnectionString("CuMusicClubDb");
        }

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new NpgsqlConnectionStringBuilder(configured)
            {
                Database = TestDatabaseName
            }.ConnectionString;
        }

        return $"Host=localhost;Database={TestDatabaseName};Username=postgres;Password=postgres";
    }
}
