using CuMusicClub.Domain.Constants;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Application.FunctionalTests.Infrastructure;

public static class TestApp
{
    private static string? _userId;
    private static List<string>? _roles;

    public static string? GetUserId() => _userId;
    public static List<string>? GetRoles() => _roles;

    public static async Task<string> RunAsDefaultUserAsync()
    {
        return await RunAsUserAsync("test@local", "Testing1234!", []);
    }

    public static async Task<string> RunAsAdministratorAsync()
    {
        return await RunAsUserAsync("administrator@local", "Administrator1234!", [Roles.Administrator]);
    }

    public static async Task<string> RunAsUserAsync(string userName, string password, string[] roles)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var user = new ApplicationUser { UserName = userName, Email = userName };
        var result = await userManager.CreateAsync(user, password);

        if (roles.Length > 0)
        {
            foreach (var role in roles)
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }

            await userManager.AddToRolesAsync(user, roles);
        }

        if (result.Succeeded)
        {
            _userId = user.Id;
            _roles = [..roles];
            return _userId;
        }

        var errors = string.Join(Environment.NewLine, result.Errors.Select(e => e.Description));
        throw new Exception($"Unable to create {userName}.{Environment.NewLine}{errors}");
    }

    public static async Task ResetState()
    {
        if (FunctionalTestSetup.DbResetter is not null)
        {
            await FunctionalTestSetup.DbResetter.ResetAsync();
        }

        _userId = null;
        _roles = null;
    }

    public static async Task<TEntity?> FindAsync<TEntity>(params object[] keyValues)
        where TEntity : class
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.FindAsync<TEntity>(keyValues);
    }

    public static async Task AddAsync<TEntity>(TEntity entity)
        where TEntity : class
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Add(entity);
        await context.SaveChangesAsync();
    }
}
