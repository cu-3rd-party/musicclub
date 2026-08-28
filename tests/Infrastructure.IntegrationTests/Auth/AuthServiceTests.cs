using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Domain.Constants;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Infrastructure.IntegrationTests.Auth;

public partial class AuthServiceTests : TestBase
{
    private sealed class AuthScope : IDisposable
    {
        private readonly IServiceScope _scope;
        public IAuthService Auth { get; }
        public UserManager<ApplicationUser> UserManager { get; }

        public AuthScope()
        {
            _scope = FunctionalTestSetup.ScopeFactory.CreateScope();
            Auth = _scope.ServiceProvider.GetRequiredService<IAuthService>();
            UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }

    private static ApplicationDbContext Db()
    {
        var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    private static async Task<(ApplicationUser AppUser, ClaimsPrincipal Principal)> CreateUserAsync(string username)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = username,
            DisplayName = $"Display {username}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var result = await userManager.CreateAsync(user, "Test1234!");
        result.Succeeded.ShouldBeTrue(
            $"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        var claims = new List<Claim>();

        claims.Add(new Claim(PermissionClaimTypes.Permission, Permission.ParticipationEditOwn));
        claims.Add(new Claim(PermissionClaimTypes.Permission, Permission.SongsEditOwn));

        foreach (var claim in claims) await userManager.AddClaimAsync(user, claim);

        var identity = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            ],
            "test");

        return (user, new ClaimsPrincipal(identity));
    }
}
