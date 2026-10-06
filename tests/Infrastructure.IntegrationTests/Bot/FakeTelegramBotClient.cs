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

    public List<SendMessageRequest> SentMessages
    {
        get
        {
            return Requests
                .OfType<SendMessageRequest>()
                .ToList();
        }
    }

    public List<AnswerCallbackQueryRequest> AnsweredCallbacks
    {
        get
        {
            return Requests
                .OfType<AnswerCallbackQueryRequest>()
                .ToList();
        }
    }

    public bool LocalBotServer
    {
        get { return false; }
    }

    public long BotId
    {
        get { return 123; }
    }

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

        // Правдоподобные ответы для запросов, результат которых использует код
        object? response = request switch
        {
            SendMessageRequest send => new Message
            {
                Id = Requests.Count,
                Chat = new Chat { Id = send.ChatId.Identifier ?? 0 },
                Text = send.Text,
            },
            CreateForumTopicRequest topic => new ForumTopic
            {
                MessageThreadId = Requests.Count,
                Name = topic.Name,
            },
            _ => null,
        };

        return Task.FromResult(response is TResponse typed ? typed : default!);
    }

    public Task<bool> TestApi(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
