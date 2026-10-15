using System.Net;
using System.Net.Sockets;
using CuMusicClub.Infrastructure.Yandex;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Infrastructure.IntegrationTests.Yandex;

public class YandexConnectRetryTests
{
    [Test]
    public async Task OpenPort_ConnectsWithoutRetries()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var buffer = new byte[1024];
            _ = await stream.ReadAsync(buffer);
            await stream.WriteAsync("HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n"u8.ToArray());
        });

        var logger = new CountingLogger();
        using var http = new HttpClient(YandexConnectRetry.CreateHandler(logger));
        using var response = await http.GetAsync($"http://localhost:{port}/");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        logger.Warnings.ShouldBe(0);
        await accept;
        listener.Stop();
    }

    [Test]
    public async Task ClosedPort_RetriesConnectThenFails()
    {
        // Свободный порт: занимаем и сразу отпускаем — подключение будет отклонено
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var logger = new CountingLogger();
        using var http = new HttpClient(YandexConnectRetry.CreateHandler(logger));

        await Should.ThrowAsync<HttpRequestException>(() => http.GetAsync($"http://127.0.0.1:{port}/"));
        logger.Warnings.ShouldBe(3); // 4 попытки: 3 повтора с предупреждением, последняя ошибка уходит наружу
    }

    private sealed class CountingLogger : ILogger
    {
        public int Warnings { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings++;
        }
    }
}
