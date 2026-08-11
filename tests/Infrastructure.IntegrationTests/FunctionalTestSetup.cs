using CuMusicClub.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CuMusicClub.Infrastructure.IntegrationTests;

[SetUpFixture]
public class FunctionalTestSetup
{
    internal static IServiceScopeFactory ScopeFactory { get; private set; } = null!;
    internal static DatabaseResetter? DbResetter { get; private set; }

    private static WebApiFactory? _factory;
    private static PostgreSqlContainer? _container;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _container = new PostgreSqlBuilder()
            .WithDatabase("musicclub_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        _factory = new WebApiFactory(_container.GetConnectionString());
        ScopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();

        using var scope = ScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();

        DbResetter = await DatabaseResetter.CreateAsync(_container.GetConnectionString());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (DbResetter is not null) await DbResetter.DisposeAsync();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }
}
