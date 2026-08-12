using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Domain.Constants;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Auth;

[TestFixture]
[TestOf(typeof(Permission))]
public class PermissionTests
{
    [Test]
    public void DefaultContainsParticipationEditOwn()
    {
        Permission.Default.ShouldContain(Permission.ParticipationEditOwn);
    }

    [Test]
    public void DefaultContainsSongsEditOwn()
    {
        Permission.Default.ShouldContain(Permission.SongsEditOwn);
    }

    [Test]
    public void DefaultHasExactlyTwoPermissions()
    {
        Permission.Default.Count.ShouldBe(2);
    }

    [Test]
    public void RoadieContainsAllDefaultPlusEditAny()
    {
        Permission.Roadie.ShouldContain(Permission.ParticipationEditOwn);
        Permission.Roadie.ShouldContain(Permission.ParticipationEditAny);
        Permission.Roadie.ShouldContain(Permission.SongsEditOwn);
        Permission.Roadie.Count.ShouldBe(3);
    }

    [Test]
    public void AllContainsAllSevenPermissions()
    {
        Permission.All.Count.ShouldBe(7);
        Permission.All.ShouldContain(Permission.ParticipationEditOwn);
        Permission.All.ShouldContain(Permission.ParticipationEditAny);
        Permission.All.ShouldContain(Permission.SongsEditOwn);
        Permission.All.ShouldContain(Permission.SongsEditAny);
        Permission.All.ShouldContain(Permission.SongsEditFeatured);
        Permission.All.ShouldContain(Permission.EventsEdit);
        Permission.All.ShouldContain(Permission.TracklistsEdit);
    }

    [Test]
    public void ByRole_Administrator_EqualsAll()
    {
        Permission
            .ByRole[Roles.Administrator]
            .ShouldBe(Permission.All);
    }

    [Test]
    public void ByRole_Roadie_EqualsRoadie()
    {
        Permission
            .ByRole[Roles.Roadie]
            .ShouldBe(Permission.Roadie);
    }

    [Test]
    public void ByRole_Default_EqualsDefault()
    {
        Permission
            .ByRole[Roles.Default]
            .ShouldBe(Permission.Default);
    }

    [Test]
    public void ByRole_ContainsThreeRoles()
    {
        Permission.ByRole.Count.ShouldBe(3);
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
            Permission.ParticipationEditOwn,
            Permission.ParticipationEditAny,
            Permission.SongsEditOwn,
            Permission.SongsEditAny,
            Permission.SongsEditFeatured,
            Permission.EventsEdit,
            Permission.TracklistsEdit,
        };
        allPermissions
            .Distinct()
            .Count()
            .ShouldBe(allPermissions.Length);
    }
}
