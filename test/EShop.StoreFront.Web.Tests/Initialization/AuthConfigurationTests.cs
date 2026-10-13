using System.Security.Claims;
using Hj.EShop.StoreFront.Web.Initialization;
using Xunit;

namespace Hj.EShop.StoreFront.Web.Tests.Initialization;

public sealed class AuthConfigurationTests
{
    [Fact]
    public void ValidateRequiredClaims_AllRequiredClaimsPresent_DoesNotThrow()
    {
        ClaimsIdentity identity = CreateIdentity();

        AuthConfiguration.ValidateRequiredClaims(identity);
    }

    [Theory]
    [InlineData("preferred_username")]
    [InlineData("email")]
    [InlineData("given_name")]
    [InlineData("family_name")]
    public void ValidateRequiredClaims_RequiredClaimMissing_ThrowsWithClaimType(string missingClaimType)
    {
        ClaimsIdentity identity = CreateIdentity(missingClaimType);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AuthConfiguration.ValidateRequiredClaims(identity));

        Assert.Contains($"'{missingClaimType}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("preferred_username")]
    [InlineData("email")]
    [InlineData("given_name")]
    [InlineData("family_name")]
    public void ValidateRequiredClaims_RequiredClaimIsWhitespace_ThrowsWithClaimType(string emptyClaimType)
    {
        ClaimsIdentity identity = CreateIdentity(emptyClaimType, " ");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AuthConfiguration.ValidateRequiredClaims(identity));

        Assert.Contains($"'{emptyClaimType}'", exception.Message, StringComparison.Ordinal);
    }

    private static ClaimsIdentity CreateIdentity(string? excludedClaimType = null, string? replacementClaimValue = null)
    {
        string[] claimTypes = ["preferred_username", "email", "given_name", "family_name"];
        ClaimsIdentity identity = new("Test");
        foreach (string claimType in claimTypes)
        {
            if (claimType == excludedClaimType && replacementClaimValue is null)
            {
                continue;
            }

            string claimValue = claimType == excludedClaimType ? replacementClaimValue! : "value";
            identity.AddClaim(new Claim(claimType, claimValue));
        }

        return identity;
    }
}
