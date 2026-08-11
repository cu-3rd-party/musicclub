using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace CuMusicClub.Infrastructure.IntegrationTests.Bot;

public class FakeTelegramBotClient : ITelegramBotClient
{
    public List<object> Requests { get; } = [];

    public List<SendMessageRequest> SentMessages =>
        Requests.OfType<SendMessageRequest>()
            .ToList();

    public List<AnswerCallbackQueryRequest> AnsweredCallbacks =>
        Requests.OfType<AnswerCallbackQueryRequest>()
            .ToList();

    public bool LocalBotServer => false;

    public long BotId => 123;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    public IExceptionParser ExceptionsParser { get; set; } = new DefaultExceptionParser();

    public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest
    {
        add { }
        remove { }
    }

    public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived
    {
        add { }
        remove { }
    }

    public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request!);
        return Task.FromResult<TResponse>(default!);
    }

    public Task<bool> TestApi(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
