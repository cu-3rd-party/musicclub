namespace CuMusicClub.Web.Infrastructure.Oidc;

public static class AuthSchemes
{
    /// <summary>Временная cookie между callback внешнего провайдера и выдачей кода входа.</summary>
    public const string External = "external";

    /// <summary>Префикс схем внешних провайдеров (OIDC и OAuth 2.0): <c>oidc-telegram</c>.</summary>
    public const string OidcPrefix = "oidc-";
}
