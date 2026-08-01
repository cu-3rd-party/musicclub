namespace CuMusicClub.Domain.ValueObjects;

public sealed record Colour
{
    public static readonly Colour Red = new("#E05C4D");
    public static readonly Colour Orange = new("#D98B2B");
    public static readonly Colour Green = new("#4CAF50");
    public static readonly Colour Teal = new("#26A69A");
    public static readonly Colour Blue = new("#5C6BC0");
    public static readonly Colour Purple = new("#AB47BC");
    public static readonly Colour Grey = new("#78909C");

    private static readonly IReadOnlySet<Colour> SupportedColours = new HashSet<Colour>
    {
        Red,
        Orange,
        Green,
        Teal,
        Blue,
        Purple,
        Grey
    };

    public string Code { get; private set; }

    private Colour() { Code = "#000000"; }

    public Colour(string code)
    {
        Code = string.IsNullOrWhiteSpace(code) ? "#000000" : code;
    }

    public static Colour From(string code)
    {
        var colour = new Colour(code);
        if (!SupportedColours.Contains(colour))
            throw new UnsupportedColourException(code);
        return colour;
    }

    public static implicit operator string(Colour colour) => colour.ToString();
    public static explicit operator Colour(string code) => From(code);

    public override string ToString() => Code;
}
