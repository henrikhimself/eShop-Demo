using Hj.ReverseProxy.Certificate;
using Microsoft.Extensions.Configuration;

namespace Hj.ReverseProxy.UnitTest.Certificate;

[Collection("EnvironmentVariable")]
public class CertificateConfigTests
{
  [Fact]
  public void GetOptions_GivenMissingCaFilePath_Throws()
  {
    // arrange
    var configuration = CreateConfiguration(new()
    {
      { "SelfSignedCertificate:AlgorithmOid", CertificateConstants.RsaOid },
      { "SelfSignedCertificate:SubjectName", "CN=Test CA" },
    });

    var sut = new CertificateConfig(configuration);

    // act & assert
    Assert.ThrowsAny<InvalidOperationException>(sut.GetOptions);
  }

  [Fact]
  public void GetOptions_GivenMissingAlgorithmOid_UseDefault()
  {
    // arrange
    var configuration = CreateConfiguration(new()
    {
      { "SelfSignedCertificate:CaFilePath", "/my/ca/path" },
      { "SelfSignedCertificate:SubjectName", "CN=Test CA" },
    });

    var sut = new CertificateConfig(configuration);

    // act
    var result = sut.GetOptions();

    // Assert
    Assert.Equal(CertificateConstants.EcdsaOid, result.AlgorithmOid);
  }

  [Fact]
  public void GetOptions_GivenMissingSubjectName_UseDefault()
  {
    // arrange
    var configuration = CreateConfiguration(new()
    {
      { "SelfSignedCertificate:CaFilePath", "/my/ca/path" },
      { "SelfSignedCertificate:AlgorithmOid", CertificateConstants.RsaOid },
    });

    var sut = new CertificateConfig(configuration);

    // act
    var result = sut.GetOptions();

    // Assert
    Assert.Equal(CertificateConstants.DefaultCaSubjectName, result.SubjectName);
  }

  [Fact]
  public void GetOptions_GivenPhysicalFilePath_UsesPathAsIs()
  {
    // arrange
    var expectedPath = "/my/ca/path";
    var configuration = CreateConfiguration(new()
    {
      { "SelfSignedCertificate:CaFilePath", expectedPath },
    });

    var sut = new CertificateConfig(configuration);

    // act
    var result = sut.GetOptions();

    // assert
    Assert.Equal(expectedPath, result.CaFilePath);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void GetOptions_GivenInvalidReverseProxyHome_Throws(string? homeValue)
  {
    // arrange
    var originalHome = Environment.GetEnvironmentVariable("REVERSEPROXY_HOME");
    try
    {
      Environment.SetEnvironmentVariable("REVERSEPROXY_HOME", homeValue);

      var configuration = CreateConfiguration(new()
      {
        { "SelfSignedCertificate:CaFilePath", "{REVERSEPROXY_HOME}" },
      });

      var sut = new CertificateConfig(configuration);

      // act & assert
      Assert.ThrowsAny<InvalidOperationException>(sut.GetOptions);
    }
    finally
    {
      Environment.SetEnvironmentVariable("REVERSEPROXY_HOME", originalHome);
    }
  }

  [Fact]
  public void GetOptions_GivenReverseProxyHome_UsesPathFromEnv()
  {
    // arrange
    var originalHome = Environment.GetEnvironmentVariable("REVERSEPROXY_HOME");
    try
    {
      Environment.SetEnvironmentVariable("REVERSEPROXY_HOME", "/custom");

      var configuration = CreateConfiguration(new()
      {
        { "SelfSignedCertificate:CaFilePath", "{REVERSEPROXY_HOME}" },
      });

      var sut = new CertificateConfig(configuration);

      // act
      var result = sut.GetOptions();

      // assert
      Assert.Equal("/custom", result.CaFilePath);
    }
    finally
    {
      Environment.SetEnvironmentVariable("REVERSEPROXY_HOME", originalHome);
    }
  }

  private static IConfiguration CreateConfiguration(Dictionary<string, string> settings)
    => new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();
}
