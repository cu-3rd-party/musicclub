using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace CuMusicClub.Web.Bot;

public class TelegramBotHostedService : BackgroundService
{
    private const string DisabledToken = "0000";

    private readonly BotOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotHostedService> _logger;

    public TelegramBotHostedService(IOptions<BotOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramBotHostedService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken) || _options.BotToken.Trim() == DisabledToken)
        {
            _logger.LogInformation("Telegram bot is not started: BotToken is '{Token}'.",
                string.IsNullOrWhiteSpace(_options.BotToken) ? "<empty>" : _options.BotToken);
            return;
        }

        var bot = new TelegramBotClient(_options.BotToken, cancellationToken: stoppingToken);

        string webAppUrl;
        try
        {
            webAppUrl = await ResolveWebAppUrlAsync(bot, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve WebApp URL; falling back to the default URL.");
            webAppUrl = _options.WebAppUrl;
        }

        try
        {
            await bot.DeleteWebhook(cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Telegram webhook (continuing with polling).");
        }

        TelegramBotClient.OnUpdateHandler onUpdate = (Update update) =>
            HandleUpdateAsync(bot, webAppUrl, update, stoppingToken);
        TelegramBotClient.OnErrorHandler onError = (Exception exception, HandleErrorSource source) =>
        {
            _logger.LogError(exception, "Telegram bot error (source: {Source})", source);
            return Task.CompletedTask;
        };

        bot.OnError += onError;
        bot.OnUpdate += onUpdate;

        _logger.LogInformation("Telegram bot is polling for updates. WebApp URL: {WebAppUrl}", webAppUrl);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            bot.OnUpdate -= onUpdate;
            bot.OnError -= onError;
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot,
        string webAppUrl,
        Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<BotUpdateHandler>();
            await handler.HandleUpdateAsync(bot, update, webAppUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle Telegram update {UpdateId}", update.Id);
        }
    }

    private async Task<string> ResolveWebAppUrlAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
    {
        try
        {
            var menuButton = await bot.GetChatMenuButton(cancellationToken: cancellationToken);
            if (menuButton is MenuButtonWebApp
                {
                    WebApp.Url:
                    {
                    } url
                })
            {
                return url;
            }

            _logger.LogWarning("Menu button is not a WebApp; falling back to default URL");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch menu button: {Message}", ex.Message);
        }

        return _options.WebAppUrl;
    }
}
