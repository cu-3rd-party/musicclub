using CuMusicClub.Domain.Exceptions;
using CuMusicClub.Domain.ValueObjects;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Domain.UnitTests.ValueObjects;

public class ColourTests
{
    [Test]
    public void ShouldReturnCorrectColourCode()
    {
        var colour = Colour.From("#E05C4D");
        colour.Code.ShouldBe("#E05C4D");
    }

    [Test]
    public void ToStringReturnsCode()
    {
        var colour = Colour.Red;
        colour.ToString().ShouldBe(colour.Code);
    }

    [Test]
    public void ShouldPerformImplicitConversionToColourCodeString()
    {
        string code = Colour.Red;
        code.ShouldBe("#E05C4D");
    }

    [Test]
    public void ShouldPerformExplicitConversionGivenSupportedColourCode()
    {
        var colour = (Colour)"#E05C4D";
        colour.ShouldBe(Colour.Red);
    }

    [Test]
    public void ShouldThrowUnsupportedColourExceptionGivenNotSupportedColourCode()
    {
        Should.Throw<UnsupportedColourException>(() => Colour.From("##FF33CC"));
    }

    [Test]
    public void ShouldBeComparable()
    {
        Colour.From("#E05C4D").ShouldBe(Colour.Red);
        new Colour("#AAAAAA").ShouldNotBe(Colour.Red);
    }
}
