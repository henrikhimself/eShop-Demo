using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hj.ReverseProxy.Abstraction;
using Hj.ReverseProxy.Certificate;
using Hj.ReverseProxy.Certificate.Strategy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Hj.ReverseProxy.UnitTest.Certificate;

public class CertificateAppTests
{
  private const string SubjectName = "CN=Unit Test CA";

  private const string TestRsaCaCrt = @"-----BEGIN CERTIFICATE-----
MIIBIzCBzqADAgECAgh1r8sQnbYIlDANBgkqhkiG9w0BAQsFADAXMRUwEwYDVQQD
EwxVbml0IFRlc3QgQ0EwHhcNMjUwNjE1MDYwOTAxWhcNMzUwNjE2MDYwOTAxWjAX
MRUwEwYDVQQDEwxVbml0IFRlc3QgQ0EwXDANBgkqhkiG9w0BAQEFAANLADBIAkEA
sY8/uATfaiA6sI3HWZiIGMx9hI8j+QNbLX41FT2OxPWD0czqEqBnpULQOODYkjxz
4HWbhTJbGBwFSKzvA2qvsQIDAQABMA0GCSqGSIb3DQEBCwUAA0EAE+Px+WgOw/in
uUBVsi6p/5BEUOWzCaF+NHX+izRFy5r3LtcSQNSRYdYmgNdrXJln76cfV0xjlXfm
qBA0aeRvOA==
-----END CERTIFICATE-----";

  private const string TestRsaCaKey = @"-----BEGIN RSA PRIVATE KEY-----
MIIBOwIBAAJBALGPP7gE32ogOrCNx1mYiBjMfYSPI/kDWy1+NRU9jsT1g9HM6hKg
Z6VC0Djg2JI8c+B1m4UyWxgcBUis7wNqr7ECAwEAAQJAGcz58lBq8m3aeVsws3kx
lYDpYEC4dm+haRvktMBsJXxVQ0mVoD6YAKqvjpUOL9F8dpcsLWDP463Cuo1zZE49
HQIhAMK/4zkfJP8+576nRpxCOEIZXvz2K59rlWGP1PSiIEeDAiEA6WdSbUsBP4E/
FAIB+wTV3mMD8UK8EuTMypXdb8gSUbsCIQCIoAfvtfrFmsMIDOBLlWVUceoiuyzl
XZth43751JeiswIgbY5MCHUObuqR2yheGZ9Za/t6HELA2PWAkw7pU9DLmIUCIQCs
HAiMP+ImbKpfNI6y9AROOlhsphQzyxgOxCPVVdeAZw==
-----END RSA PRIVATE KEY-----";

  private const string TestEcdsaCaCrt = @"-----BEGIN CERTIFICATE-----
MIIBITCByaADAgECAgkA0epGWb7QAEEwCgYIKoZIzj0EAwIwFzEVMBMGA1UEAxMM
VW5pdCBUZXN0IENBMB4XDTI1MDYxNTA2MDQ0NloXDTM1MDYxNjA2MDQ0NlowFzEV
MBMGA1UEAxMMVW5pdCBUZXN0IENBMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE
mt3pWSUy5hsYy+c1gr6Hii9i62g1xaIvVudqUQYnL4aiKeCYaBhOaSkb7U6H7JZH
gRuXcDt0/uIx/bguxUNKGTAKBggqhkjOPQQDAgNHADBEAiBIKQRfinS1RgTKNkp3
vtBZXkyTPqvZB/rOSlZBYGxsJAIgDerVd87SYQ3hmgfxDtwdHEQNKOTB4i5rK75u
JkBAikc=
-----END CERTIFICATE-----";

  private const string TestEcdsaCaKey = @"-----BEGIN EC PRIVATE KEY-----
MHcCAQEEIJOVKzjwE1wFw+fEGCOmeiIOGm2u+MZGbwZrTe5NLbUmoAoGCCqGSM49
AwEHoUQDQgAEmt3pWSUy5hsYy+c1gr6Hii9i62g1xaIvVudqUQYnL4aiKeCYaBhO
aSkb7U6H7JZHgRuXcDt0/uIx/bguxUNKGQ==
-----END EC PRIVATE KEY-----";

  [Theory]
  [InlineData(CertificateConstants.RsaOid, TestRsaCaCrt, TestRsaCaKey)]
  [InlineData(CertificateConstants.EcdsaOid, TestEcdsaCaCrt, TestEcdsaCaKey)]
  public void GetCertificate_GivenDnsName_ReturnsCertificate(string algorithmOid, string caCrtPem, string caKeyPem)
  {
    // arrange
    IFileStore? fileStore = null;

    var sut = SystemUnderTest.For<CertificateApp>(arrange =>
    {
      var settings = arrange.Instance<Dictionary<string, string>>();
      settings.Add("SelfSignedCertificate:AlgorithmOid", algorithmOid);

      SetHappyPath(arrange);

      fileStore = arrange.Instance<IFileStore>();
      fileStore.ReadAllText(Arg.Is<string>(x => Path.GetFileName(x) == CertificateConstants.DefaultCaName + CertificateConstants.CaCrtFileExtension)).Returns(caCrtPem);
      fileStore.ReadAllText(Arg.Is<string>(x => Path.GetFileName(x) == CertificateConstants.DefaultCaName + CertificateConstants.CaKeyFileExtension)).Returns(caKeyPem);
    });

    // act
    var result = sut.GetCertificate("example.com");

    // assert
    Assert.Equal("CN=example.com", result.Subject);
    Assert.Equal(SubjectName, result.Issuer);
    Assert.Equal(algorithmOid, result.PublicKey.Oid.Value);

    var constraints = result.Extensions.OfType<X509BasicConstraintsExtension>().Single();
    Assert.False(constraints.CertificateAuthority);

    var usage = result.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages;
    Assert.Contains(usage.Cast<Oid>(), x => x.Value == "1.3.6.1.5.5.7.3.1");

    fileStore!.Received(2).ReadAllText(Arg.Any<string>()); // ca crt and key
  }

  [Theory]
  [InlineData(CertificateConstants.RsaOid)]
  [InlineData(CertificateConstants.EcdsaOid)]
  public void GetCertificate_GivenMissingCa_CreatesCa(string algorithmOid)
  {
    // arrange
    IFileStore? fileStore = null;

    var sut = SystemUnderTest.For<CertificateApp>(arrange =>
    {
      var settings = arrange.Instance<Dictionary<string, string>>();
      settings.Add("SelfSignedCertificate:AlgorithmOid", algorithmOid);

      SetHappyPath(arrange);

      fileStore = arrange.Instance<IFileStore>();
      fileStore.FileExists(Arg.Any<string>()).Returns(false);
    });

    // act
    var result = sut.GetCertificate("example.com");

    // assert
    Assert.Equal(SubjectName, result.Issuer);
    Assert.Equal(algorithmOid, result.PublicKey.Oid.Value);

    fileStore!.Received(2).WriteAllText(Arg.Any<string>(), Arg.Any<string>()); // ca crt and key
    fileStore!.Received(1).WriteAllBytes(Arg.Any<string>(), Arg.Any<byte[]>()); // ca pfx
  }

  [Fact]
  public void GetCertificate_GivenMissingStrategy_Throws()
  {
    // arrange
    var sut = SystemUnderTest.For<CertificateApp>(arrange =>
    {
      var settings = arrange.Instance<Dictionary<string, string>>();
      settings.Add("SelfSignedCertificate:AlgorithmOid", "oid that does not exist");

      SetHappyPath(arrange);
    });

    // act & asset
    Assert.ThrowsAny<NotSupportedException>(() => sut.GetCertificate("www.example.com"));
  }

  private static void SetHappyPath(InputBuilder arrange)
  {
    var settings = arrange.Instance<Dictionary<string, string>>()!;
    settings.TryAdd("SelfSignedCertificate:CaFilePath", "/my/ca/path");
    settings.TryAdd("SelfSignedCertificate:AlgorithmOid", CertificateConstants.RsaOid);
    settings.TryAdd("SelfSignedCertificate:SubjectName", SubjectName);

    arrange.Advanced.Instance(() => new ConfigurationBuilder().AddInMemoryCollection(settings!).Build());
    arrange.Instance<ICertificateConfig, CertificateConfig>();

    var memoryCache = arrange.Instance<IMemoryCache>();
    memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object?>()).Returns(args =>
    {
      args[1] = null;
      return false;
    });

    arrange.Instance<CertificateEcdsa>();
    arrange.Instance<CertificateRsa>();

#if NET8_0
    if (OperatingSystem.IsMacOS())
    {
      arrange.Instance<ICaLoader, MacOSNet8CaLoader>();
      arrange.Instance<ICertificateCreator, MacOSNet8CertificateCreator>();
    }
    else
    {
      arrange.Instance<ICaLoader, DefaultCaLoader>();
      arrange.Instance<ICertificateCreator, DefaultCertificateCreator>();
    }
#else
    arrange.Instance<ICaLoader, DefaultCaLoader>();
    arrange.Instance<ICertificateCreator, DefaultCertificateCreator>();
#endif

    var fileStore = arrange.Instance<IFileStore>();
    fileStore.CombinePath(Arg.Any<string>(), Arg.Any<string>()).Returns(args => Path.Combine(args.ArgAt<string>(0), args.ArgAt<string>(1)));
    fileStore.GetFullPath(Arg.Any<string>()).Returns(args => args.ArgAt<string>(0));
    fileStore.FileExists(Arg.Any<string>()).Returns(true);
    fileStore.DirectoryExists(Arg.Any<string>()).Returns(true);
    fileStore.ReadAllText(Arg.Is<string>(x => Path.GetFileName(x) == CertificateConstants.DefaultCaName + CertificateConstants.CaCrtFileExtension)).Returns(TestRsaCaCrt);
    fileStore.ReadAllText(Arg.Is<string>(x => Path.GetFileName(x) == CertificateConstants.DefaultCaName + CertificateConstants.CaKeyFileExtension)).Returns(TestRsaCaKey);
  }
}
