using CuMusicClub.Application.Common.Exceptions;
using FluentValidation.Results;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Common.Exceptions;

public class ValidationExceptionTests
{
    [Test]
    public void DefaultConstructorCreatesAnEmptyErrorDictionary()
    {
        var actual = new ValidationException().Errors;

        actual.Keys.ShouldBeEmpty();
    }

    [Test]
    public void SingleValidationFailureCreatesASingleElementErrorDictionary()
    {
        var failures = new List<ValidationFailure>
        {
            new("Age", "must be over 18"),
        };

        var actual = new ValidationException(failures).Errors;

        actual.Keys.ShouldBe(new string[]
        {
            "Age",
        });
        actual["Age"]
            .ShouldBe(new string[]
            {
                "must be over 18",
            });
    }

    [Test]
    public void
        MulitpleValidationFailureForMultiplePropertiesCreatesAMultipleElementErrorDictionaryEachWithMultipleValues()
    {
        var failures = new List<ValidationFailure>
        {
            new("Age", "must be 18 or older"),
            new("Age", "must be 25 or younger"),
            new("Password", "must contain at least 8 characters"),
            new("Password", "must contain a digit"),
            new("Password", "must contain upper case letter"),
            new("Password", "must contain lower case letter"),
        };

        var actual = new ValidationException(failures).Errors;

        actual.Keys.ShouldBe(new string[]
            {
                "Password",
                "Age",
            },
            true);

        actual["Age"]
            .ShouldBe(new string[]
                {
                    "must be 25 or younger",
                    "must be 18 or older",
                },
                true);

        actual["Password"]
            .ShouldBe(new string[]
                {
                    "must contain lower case letter",
                    "must contain upper case letter",
                    "must contain at least 8 characters",
                    "must contain a digit",
                },
                true);
    }
}
