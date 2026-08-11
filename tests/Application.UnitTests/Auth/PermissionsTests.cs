using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Domain.Constants;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Auth;

[TestFixture]
[TestOf(typeof(Permissions))]
public class PermissionsTests
{
    [Test]
    public void DefaultContainsParticipationEditOwn()
    {
        Permissions.Default.ShouldContain(Permissions.ParticipationEditOwn);
    }

    [Test]
    public void DefaultContainsSongsEditOwn()
    {
        Permissions.Default.ShouldContain(Permissions.SongsEditOwn);
    }

    [Test]
    public void DefaultHasExactlyTwoPermissions()
    {
        Permissions.Default.Count.ShouldBe(2);
    }

    [Test]
    public void RoadieContainsAllDefaultPlusEditAny()
    {
        Permissions.Roadie.ShouldContain(Permissions.ParticipationEditOwn);
        Permissions.Roadie.ShouldContain(Permissions.ParticipationEditAny);
        Permissions.Roadie.ShouldContain(Permissions.SongsEditOwn);
        Permissions.Roadie.Count.ShouldBe(3);
    }

    [Test]
    public void AllContainsAllSevenPermissions()
    {
        Permissions.All.Count.ShouldBe(7);
        Permissions.All.ShouldContain(Permissions.ParticipationEditOwn);
        Permissions.All.ShouldContain(Permissions.ParticipationEditAny);
        Permissions.All.ShouldContain(Permissions.SongsEditOwn);
        Permissions.All.ShouldContain(Permissions.SongsEditAny);
        Permissions.All.ShouldContain(Permissions.SongsEditFeatured);
        Permissions.All.ShouldContain(Permissions.EventsEdit);
        Permissions.All.ShouldContain(Permissions.TracklistsEdit);
    }

    [Test]
    public void ByRole_Administrator_EqualsAll()
    {
        Permissions
            .ByRole[Roles.Administrator]
            .ShouldBe(Permissions.All);
    }

    [Test]
    public void ByRole_Roadie_EqualsRoadie()
    {
        Permissions
            .ByRole[Roles.Roadie]
            .ShouldBe(Permissions.Roadie);
    }

    [Test]
    public void ByRole_Default_EqualsDefault()
    {
        Permissions
            .ByRole[Roles.Default]
            .ShouldBe(Permissions.Default);
    }

    [Test]
    public void ByRole_ContainsThreeRoles()
    {
        Permissions.ByRole.Count.ShouldBe(3);
    }

    [Test]
    public void PermissionClaimType_IsPermission()
    {
        PermissionClaimTypes.Permission.ShouldBe("permission");
    }

    [Test]
    public void AllPermissionStrings_AreUnique()
    {
        var allPermissions = new[]
        {
            Permissions.ParticipationEditOwn,
            Permissions.ParticipationEditAny,
            Permissions.SongsEditOwn,
            Permissions.SongsEditAny,
            Permissions.SongsEditFeatured,
            Permissions.EventsEdit,
            Permissions.TracklistsEdit,
        };
        allPermissions
            .Distinct()
            .Count()
            .ShouldBe(allPermissions.Length);
    }
}
