using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CuMusicClub.Application.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Infrastructure.IntegrationTests.Web;

/// <summary>Ошибки API приходят как ProblemDetails с русским текстом, который фронт показывает пользователю.</summary>
public class ProblemDetailsTests : TestBase
{
    private const string Songs = "/api/v1/songs";

    private static async Task<HttpClient> AuthorizedClientAsync()
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var logins = scope.ServiceProvider.GetRequiredService<IExternalLoginService>();
        var user = await logins.LoginAsync(new ExternalIdentity("telegram", "problem-details", "Анна", "anna"));
        var code = logins.IssueLoginCode(user.Id);

        var client = FunctionalTestSetup.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/external/exchange", new { code });
        var session = await response.Content.ReadFromJsonAsync<AuthSessionDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private static object Song(string title = "Песня", string artist = "Группа", string url = "https://youtu.be/abc") =>
        new { title, artist, url, featured = false };

    private static async Task<ValidationProblemDetails> ReadProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
    }

    [Test]
    public async Task CreateSong_WithUnsupportedUrl_ExplainsWhichLinksWork()
    {
        using var client = await AuthorizedClientAsync();

        var response = await client.PostAsJsonAsync(Songs, Song(url: "https://open.spotify.com/track/123"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadProblemAsync(response);
        problem.Title.ShouldBe("Некорректный запрос");
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("YouTube, Яндекс Музыку или SoundCloud");
        problem.Errors.ShouldContainKey("url");
    }

    [Test]
    public async Task CreateSong_WithoutTitle_ReturnsFieldError()
    {
        using var client = await AuthorizedClientAsync();

        var response = await client.PostAsJsonAsync(Songs, Song(title: "  "));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadProblemAsync(response);
        problem.Detail.ShouldBe("Укажите название песни.");
        problem.Errors["title"].ShouldBe(["Укажите название песни."]);
    }

    [Test]
    public async Task GetSong_Unknown_ReturnsRussianNotFound()
    {
        using var client = await AuthorizedClientAsync();

        var response = await client.GetAsync($"{Songs}/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await ReadProblemAsync(response);
        problem.Title.ShouldBe("Не найдено");
        problem.Detail.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Exchange_WithUnknownCode_ExplainsWhatToDo()
    {
        using var client = FunctionalTestSetup.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/external/exchange", new { code = "nope" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblemAsync(response)).Detail.ShouldBe("Код входа недействителен или уже использован — войдите ещё раз.");
    }
}
