namespace CuMusicClub.Domain.Entities;

public class RefreshToken
{
    public Guid Sub { get; set; }
    public Guid Jti { get; set; }
    public DateTimeOffset Exp { get; set; }
    public DateTimeOffset Iat { get; set; }

    public bool Revoked { get; set; }

    public ApplicationUser? SubUser;
    public UserSession? JtiSession;
}
