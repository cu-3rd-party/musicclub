using CuMusicClub.Application.Services.Song;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;

namespace CuMusicClub.Web.Backfill;

public sealed class TopicBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<TopicBackfillHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Topic backfill failed");
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var songs = scope.ServiceProvider.GetRequiredService<ISongRepository>();
        var songTopics = scope.ServiceProvider.GetRequiredService<ISongTopicRepository>();

        var filledSongs = await songs.GetFilledSongIdsWithoutTopicAsync(cancellationToken);
        if (filledSongs.Count == 0)
        {
            logger.LogDebug("Topic backfill: no filled songs without a topic");
            return;
        }

        var created = 0;
        foreach (var song in filledSongs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var existing = await songTopics.FindBySongIdAsync(song.Id, cancellationToken);
            if (existing != null) continue;

            try
            {
                await new SongServiceTopics(scope.ServiceProvider.GetRequiredService<ITelegramChatService>())
                    .CreateTopicForFullSongAsync(song, cancellationToken);
                created++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Topic backfill: failed to create topic for song {SongId} ({Title})",
                    song.Id, song.Title);
            }
        }

        if (created > 0)
            logger.LogInformation("Topic backfill: created {Count} topics for filled songs", created);
        else
            logger.LogDebug("Topic backfill: no topics created");
    }
}
