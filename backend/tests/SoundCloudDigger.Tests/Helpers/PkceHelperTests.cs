using SoundCloudDigger.Api.Helpers;

namespace SoundCloudDigger.Tests.Helpers;

public class PkceHelperTests
{
    [Fact]
    public void GenerateCodeVerifier_Returns43To128CharBase64UrlString()
    {
        var verifier = PkceHelper.GenerateCodeVerifier();
        Assert.InRange(verifier.Length, 43, 128);
        Assert.Matches("^[A-Za-z0-9_-]+$", verifier);
    }

    [Fact]
    public void GenerateCodeChallenge_ReturnsSha256Base64UrlHash()
    {
        var verifier = "test_verifier_string_for_pkce_challenge";
        var challenge = PkceHelper.GenerateCodeChallenge(verifier);

        Assert.NotEmpty(challenge);
        Assert.Matches("^[A-Za-z0-9_-]+$", challenge);
        Assert.DoesNotContain("=", challenge);
    }

    [Fact]
    public void GenerateCodeChallenge_IsDeterministic()
    {
        var verifier = "deterministic_test";
        var challenge1 = PkceHelper.GenerateCodeChallenge(verifier);
        var challenge2 = PkceHelper.GenerateCodeChallenge(verifier);
        Assert.Equal(challenge1, challenge2);
    }

    [Fact]
    public void GenerateCodeVerifier_ProducesUniqueValues()
    {
        var v1 = PkceHelper.GenerateCodeVerifier();
        var v2 = PkceHelper.GenerateCodeVerifier();
        Assert.NotEqual(v1, v2);
    }
}
