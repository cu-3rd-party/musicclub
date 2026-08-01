namespace CuMusicClub.Domain.Entities;

public class ShortenedUrl
{
    public Guid Id { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
    public string? CreatedBy { get; set; }
}
