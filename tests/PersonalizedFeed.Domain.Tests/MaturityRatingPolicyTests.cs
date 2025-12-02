using PersonalizedFeed.Domain.Policies;
using Shouldly;

namespace PersonalizedFeed.Domain.Tests;

public class MaturityRatingPolicyTests
{
    [Theory]
    [InlineData("G", "PG", true)]
    [InlineData("PG", "PG", true)]
    [InlineData("PG13", "PG13", true)]
    [InlineData("PG-13", "PG13", true)]
    [InlineData("R", "PG13", false)]
    [InlineData("NC17", "PG13", false)]
    public void IsAllowed_RespectsPolicyOrdering(string videoRating, string policyRating, bool expected)
    {
        // Act
        var allowed = MaturityRatingPolicy.IsAllowed(videoRating, policyRating);

        // Assert
        allowed.ShouldBe(expected);
    }

    [Fact]
    public void IsAllowed_TreatsUnknownRatingAsMostRestrictive()
    {
        // Arrange
        var videoRating = "???";        // bad / unknown rating from CMS
        var policyRating = "PG13";

        // Act
        var allowed = MaturityRatingPolicy.IsAllowed(videoRating, policyRating);

        // Assert
        allowed.ShouldBeFalse();
    }

    [Fact]
    public void IsAllowed_AllowsEverythingForAdultPolicy()
    {
        // Arrange
        var policyRating = "NC17";

        // Act & Assert
        MaturityRatingPolicy.IsAllowed("G", policyRating).ShouldBeTrue();
        MaturityRatingPolicy.IsAllowed("PG", policyRating).ShouldBeTrue();
        MaturityRatingPolicy.IsAllowed("PG13", policyRating).ShouldBeTrue();
        MaturityRatingPolicy.IsAllowed("R", policyRating).ShouldBeTrue();
        MaturityRatingPolicy.IsAllowed("NC17", policyRating).ShouldBeTrue();
    }
}
