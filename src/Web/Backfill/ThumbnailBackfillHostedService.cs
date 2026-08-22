using System.Security.Cryptography;
using CuMusicClub.Application.Services.DataEntry;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;

namespace CuMusicClub.Web.Backfill;

public sealed class ThumbnailBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<ThumbnailBackfillHostedService> logger) : IHostedService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10); // могу себе позволить

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunBatchAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Thumbnail backfill run failed");
            }

            try
            {
                await Task.Delay(Interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Starting thumbnail backfill run");

        var succeeded = 0;
        var failed = 0;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dataEntryService = scope.ServiceProvider.GetRequiredService<IDataEntryService>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var songs = await db
                .Songs.Where(s =>
                    s.ThumbnailDataEntryId == null && s.ThumbnailUrl != null && s.ThumbnailUrl.Trim() != "")
                .OrderBy(s => s.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (songs.Count == 0) break;

            logger.LogDebug("Processing batch of {Count} thumbnails", songs.Count);

            foreach (var song in songs)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var trimmedUrl = song.ThumbnailUrl!.Trim();
                if (string.IsNullOrEmpty(trimmedUrl) ||
                    !Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                {
                    logger.LogWarning("Skipping song {SongId} ({Title}): invalid thumbnail URL '{Url}'",
                        song.Id,
                        song.Title,
                        song.ThumbnailUrl);
                    continue;
                }

                try
                {
                    var dataEntry = await GetOrCreateDataEntryAsync(dataEntryService, trimmedUrl, cancellationToken);
                    song.ThumbnailUrl = $"/data/{dataEntry.Id}";
                    song.ThumbnailDataEntryId = dataEntry.Id;
                    succeeded++;
                    logger.LogDebug("Migrated thumbnail for song {SongId} ({Title})", song.Id, song.Title);
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(ex,
                        "Failed to migrate thumbnail for song {SongId} ({Title}) from {Url}",
                        song.Id,
                        song.Title,
                        song.ThumbnailUrl);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        if (succeeded != 0)
            logger.LogInformation("Thumbnail backfill run finished. Succeeded: {Succeeded}, Failed: {Failed}",
                succeeded,
                failed);
        else
            logger.LogDebug("Thumbnail backfill run finished. Succeeded: {Succeeded}, Failed: {Failed}",
                succeeded,
                failed);
    }

    private async Task<DataEntry> GetOrCreateDataEntryAsync(
        IDataEntryService dataEntryService,
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (string.IsNullOrWhiteSpace(contentType))
            throw new InvalidOperationException($"Thumbnail response from '{url}' does not contain a Content-Type.");

        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Thumbnail response from '{url}' has unexpected Content-Type '{contentType}'.");

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var dataEntry = await dataEntryService.Create(content, contentType, cancellationToken);

        return dataEntry;
    }
}
