using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CuMusicClub.Infrastructure.IntegrationTests.Infrastructure;

public class WebApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public WebApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CuMusicClubDb", _connectionString);
    }
}
