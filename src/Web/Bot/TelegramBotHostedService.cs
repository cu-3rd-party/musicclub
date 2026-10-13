using CuMusicClub.Application.Common.Options;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CuMusicClub.Web.Bot;

public class TelegramBotHostedService : BackgroundService
{
    private const string DisabledToken = "0000";

    private readonly BotOptions _options;
    private readonly TelegramUpdateQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotHostedService> _logger;

    public TelegramBotHostedService(IOptions<BotOptions> options,
        TelegramUpdateQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramBotHostedService> logger)
    {
        _options = options.Value;
        _queue = queue;
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

        await RegisterCommandsAsync(bot, stoppingToken);

        if (_options.IsEnabled())
            await RunWebhookAsync(bot, webAppUrl, stoppingToken);
        else
            await RunPollingAsync(bot, webAppUrl, stoppingToken);
    }

    /// <summary>
    /// Вебхук: Telegram шлёт апдейты на TelegramWebhook.Path, эндпоинт кладёт их в очередь, здесь — обработка.
    /// Пока под перезапускается, Telegram копит апдейты у себя и доставит их новому экземпляру.
    /// </summary>
    private async Task RunWebhookAsync(TelegramBotClient bot, string webAppUrl, CancellationToken stoppingToken)
    {
        BotMetrics.TrackQueue(_queue);

        try
        {
            await bot.SetWebhook(_options.WebhookUrl,
                allowedUpdates: [UpdateType.Message, UpdateType.CallbackQuery],
                secretToken: _options.ResolveSecret(),
                cancellationToken: stoppingToken);
            _logger.LogInformation("Telegram webhook set to {WebhookUrl}. WebApp URL: {WebAppUrl}",
                _options.WebhookUrl, webAppUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Остаётся прежний вебхук (если был) — апдейты продолжат приходить, если URL не менялся
            _logger.LogError(ex, "Failed to set Telegram webhook to {WebhookUrl}", _options.WebhookUrl);
            BotMetrics.PollingError("SetWebhook");
        }

        try
        {
            await foreach (var update in _queue.Reader.ReadAllAsync(stoppingToken))
                await HandleUpdateAsync(bot, webAppUrl, update, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunPollingAsync(TelegramBotClient bot, string webAppUrl, CancellationToken stoppingToken)
    {
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
            BotMetrics.PollingError(source.ToString());
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
        var type = update.Type.ToString();
        using var timer = BotMetrics.MeasureUpdate(type);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<BotUpdateHandler>();
            await handler.HandleUpdateAsync(bot, update, webAppUrl, cancellationToken);
            BotMetrics.UpdateHandled(type, success: true);
        }
        catch (Exception ex)
        {
            BotMetrics.UpdateHandled(type, success: false);
            _logger.LogError(ex, "Failed to handle Telegram update {UpdateId}", update.Id);
        }
    }

    /// <summary>
    /// Меню команд с подсказками: в личке — вход и справка, в группах — команды для топиков песен.
    /// </summary>
    private async Task RegisterCommandsAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
    {
        BotCommand[] privateCommands =
        [
            new() { Command = "start", Description = "Открыть приложение Music Club" },
            new() { Command = "help", Description = "Список команд" },
        ];

        BotCommand[] groupCommands =
        [
            new() { Command = "slots", Description = "Свободные окна на неделю" },
            new() { Command = "slots_with", Description = "Окна, когда доступен роуди" },
            new() { Command = "check", Description = "Проверить время: /check 19.02 18:00" },
            new() { Command = "take", Description = "Забронировать: /take 19.02 18:00" },
            new() { Command = "take_with", Description = "Заявка с роуди: /take_with 19.02 18:00" },
            new() { Command = "cancel", Description = "Отменить бронь: /cancel 19.02 18:00" },
            new() { Command = "day", Description = "Расписание дня картинкой" },
            new() { Command = "status", Description = "Брони на день" },
            new() { Command = "ping", Description = "Позвать участников песни" },
            new() { Command = "call_my_roadie", Description = "Позвать роуди песни" },
            new() { Command = "ticket_roadie", Description = "Попросить роуди для группы" },
            new() { Command = "roadie", Description = "Позвать всех роуди" },
            new() { Command = "help", Description = "Список команд" },
        ];

        try
        {
            await bot.SetMyCommands(privateCommands, new BotCommandScopeAllPrivateChats(),
                cancellationToken: cancellationToken);
            await bot.SetMyCommands(groupCommands, new BotCommandScopeAllGroupChats(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register bot commands menu.");
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
                    } url,
                })
                return url;

            _logger.LogWarning("Menu button is not a WebApp; falling back to default URL");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch menu button: {Message}", ex.Message);
        }

        return _options.WebAppUrl;
    }
}
