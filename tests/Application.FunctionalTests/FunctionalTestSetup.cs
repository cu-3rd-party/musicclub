using CuMusicClub.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Application.FunctionalTests;

[SetUpFixture]
public class FunctionalTestSetup
{
    internal static IServiceScopeFactory ScopeFactory { get; private set; } = null!;
    internal static DatabaseResetter? DbResetter { get; private set; }

    private static WebApiFactory? _factory;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WebApiFactory();
        ScopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();

        using var scope = ScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        DbResetter = await DatabaseResetter.CreateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (DbResetter is not null) await DbResetter.DisposeAsync();
        if (_factory is not null) await _factory.DisposeAsync();
    }
}
