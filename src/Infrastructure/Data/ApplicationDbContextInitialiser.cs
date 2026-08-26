using System.Data;
using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Domain.Constants;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CuMusicClub.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}

public class ApplicationDbContextInitialiser(
    ILogger<ApplicationDbContextInitialiser> logger,
    ApplicationDbContext db,
    IHostEnvironment env,
    IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString(Shared.Services.Database) ??
                                                throw new InvalidOperationException(
                                                    $"Connection string '{Shared.Services.Database}' not found.");

    public async Task InitialiseAsync()
    {
        try
        {
            if (env.IsDevelopment()) await db.Database.EnsureDeletedAsync();

            await EnsureDatabaseExistsAsync();

            await MigrateOrCreateSchemaAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    private async Task MigrateOrCreateSchemaAsync()
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        if (pending.Count == 0)
        {
            await db.Database.EnsureCreatedAsync();
            return;
        }

        // databases created before migrations were introduced have no history table;
        // migrating them would try to re-create existing tables
        if (!await AnyTableExistsAsync(MigrationsHistoryTable) && await AnyTableExistsAsync())
        {
            logger.LogWarning(
                "Skipping automatic migration: database schema exists without migrations history. " +
                "Apply migrations manually or reset the database.");
            return;
        }

        logger.LogInformation("Applying {Count} pending database migration(s)...", pending.Count);
        await db.Database.MigrateAsync();
    }

    private async Task EnsureDatabaseExistsAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        var databaseName = builder.Database;
        builder.Database = "postgres";

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
        checkCommand.Parameters.AddWithValue("name", databaseName ?? string.Empty);

        if (await checkCommand.ExecuteScalarAsync() is not null) return;

        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await createCommand.ExecuteNonQueryAsync();
    }

    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    private async Task<bool> AnyTableExistsAsync(string? tableName = null)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = tableName is null
                ? "SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' LIMIT 1"
                : "SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @name";
            if (tableName is not null) command.Parameters.AddWithValue("name", tableName);

            return await command.ExecuteScalarAsync() is not null;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        // do nothing operation uhh placeholder no need to seed
    }
}
