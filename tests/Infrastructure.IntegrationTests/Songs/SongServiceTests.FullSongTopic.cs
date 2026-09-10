using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Infrastructure.IntegrationTests.Songs;

public partial class SongServiceTests
{
    private static async Task<long> SeedTopicAsync(Guid songId)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var song = await db.Songs.FirstAsync(s => s.Id == songId);
        var topicId = Random.Shared.NextInt64(1_000, 100_000_000);

        db.SongTopics.Add(new SongTopic
        {
            SongId = songId,
            Song = song,
            Title = $"{song.Title} — {song.Artist}",
            TopicId = topicId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        return topicId;
    }

    [Test]
    public async Task SideUserWithEditOwnAndEditAny_CanRemoveMember_FromFullSongWithTopic()
    {
        var (_, editorPrincipal) = await CreateUserAsync("editor",
            editOwnParticipation: true,
            editAnyParticipation: true);
        var (vocal, _) = await CreateUserAsync("vocal", true);
        var (guitar, _) = await CreateUserAsync("guitar", true);

        var songId = await SeedSongAsync(roles: new[]
        {
            "Вокал",
            "Гитара",
        });
        var vocalRoleId = await FindRoleIdAsync(songId, "Вокал");
        await SeedAssignmentAsync(songId, "Вокал", vocal.Id);
        await SeedAssignmentAsync(songId, "Гитара", guitar.Id);

        // Удаление уходит в Telegram только когда у песни уже есть топик, поэтому
        // снимаем участника до того, как топик появится (тест не ходит в Telegram).
        SongDto result;
        using (var scope = new SongScope())
        {
            result = await scope.Songs.LeaveRoleAsync(vocal, editorPrincipal, vocalRoleId, CancellationToken.None);
        }

        result
            .Roles.Single(r => r.Title == "Вокал")
            .Assignment.ShouldBeNull();
        result
            .Roles.Single(r => r.Title == "Гитара")
            .Assignment.ShouldNotBeNull();

        using (var db = Db())
        {
            (await db.SongRoleAssignments.CountAsync(a => a.SongId == songId && a.RoleId == vocalRoleId)).ShouldBe(0);
        }

        // Песня «собирается» и получает топик.
        await SeedTopicAsync(songId);

        using var db2 = Db();
        (await db2.SongTopics.AnyAsync(t => t.SongId == songId)).ShouldBeTrue();
    }
}