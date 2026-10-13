using System.Net;
using System.Text;
using CuMusicClub.Application.Common.Options;
using CuMusicClub.Web.Bot;
using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Infrastructure.IntegrationTests.Bot;

public class TelegramWebhookTests : TestBase
{
    private const string Update = """{"update_id":42,"message":{"message_id":1,"date":0,"chat":{"id":1,"type":"private"},"text":"/help"}}""";

    private static TelegramUpdateQueue Queue =>
        FunctionalTestSetup.Factory.Services.GetRequiredService<TelegramUpdateQueue>();

    private static async Task<HttpStatusCode> PostAsync(string body, string? secret)
    {
        using var client = FunctionalTestSetup.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, TelegramWebhook.Path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (secret != null) request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static void Drain()
    {
        while (Queue.Reader.TryRead(out _))
        {
        }
    }

    [Test]
    public async Task ValidSecret_EnqueuesUpdate()
    {
        Drain();

        (await PostAsync(Update, WebApiFactory.TestWebhookSecret)).ShouldBe(HttpStatusCode.OK);

        Queue.Reader.TryRead(out var update).ShouldBeTrue();
        update!.Id.ShouldBe(42);
        update.Message!.Text.ShouldBe("/help");
    }

    [TestCase(null)]
    [TestCase("wrong")]
    public async Task WrongSecret_Unauthorized(string? secret)
    {
        Drain();

        (await PostAsync(Update, secret)).ShouldBe(HttpStatusCode.Unauthorized);

        Queue.Count.ShouldBe(0);
    }

    [Test]
    public async Task InvalidJson_BadRequest()
    {
        (await PostAsync("{not json", WebApiFactory.TestWebhookSecret)).ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public void Secret_DerivedFromTokenWhenNotConfigured()
    {
        var a = new BotOptions { BotToken = "123:abc" }.ResolveSecret();

        a.ShouldBe(new BotOptions { BotToken = "123:abc" }.ResolveSecret());
        a.ShouldNotBe(new BotOptions { BotToken = "123:abd" }.ResolveSecret());
        a.ShouldMatch("^[A-Za-z0-9_-]{1,256}$");
        new BotOptions { BotToken = "123:abc", WebhookSecret = "explicit" }.ResolveSecret().ShouldBe("explicit");
    }
}
