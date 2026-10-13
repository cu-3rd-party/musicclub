using System.Threading.Channels;
using Telegram.Bot.Types;

namespace CuMusicClub.Web.Bot;

/// <summary>
/// Очередь апдейтов из вебхука: эндпоинт сразу отвечает Telegram 200, обработка — в TelegramBotHostedService.
/// </summary>
public sealed class TelegramUpdateQueue
{
    private const int Capacity = 1000;

    private readonly Channel<Update> _channel = Channel.CreateBounded<Update>(
        new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public ChannelReader<Update> Reader => _channel.Reader;

    public int Count => _channel.Reader.Count;

    /// <returns>false, если очередь переполнена — Telegram повторит доставку.</returns>
    public bool TryEnqueue(Update update) => _channel.Writer.TryWrite(update);
}
