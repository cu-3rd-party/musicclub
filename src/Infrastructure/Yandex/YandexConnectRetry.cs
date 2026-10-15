using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Повтор установки TCP-соединения с calendar.yandex.ru. С октября 2026 часть подключений с IP узла grm
///     к календарю не устанавливается (SYN теряется, ~40%), остальные хосты Яндекса доступны.
///     Повторяется только connect: запрос ещё не отправлен, поэтому create-event не может создаться дважды.
/// </summary>
public static class YandexConnectRetry
{
    private const int Attempts = 4;
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);

    public static SocketsHttpHandler CreateHandler(ILogger logger)
    {
        return new SocketsHttpHandler
        {
            // Cookie шлём руками из файла, редирект на passport ловим как «сессия протухла»
            UseCookies = false,
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = (context, ct) => ConnectAsync(context, logger, ct)
        };
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, ILogger logger,
        CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(AttemptTimeout);
            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, timeout.Token);
                if (attempt > 1)
                    logger.LogInformation("🔌 {Host}: соединение установлено с {Attempt}-й попытки",
                        context.DnsEndPoint.Host, attempt);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (attempt < Attempts && !ct.IsCancellationRequested
                                       && ex is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                logger.LogWarning("🔌 {Host}: не удалось подключиться (попытка {Attempt}/{Attempts}): {Reason}",
                    context.DnsEndPoint.Host, attempt, Attempts,
                    ex is OperationCanceledException ? $"таймаут {AttemptTimeout.TotalSeconds:0} с" : ex.Message);
                await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), ct);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}
