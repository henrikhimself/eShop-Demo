// <copyright file="DefaultCertificateCreator.cs" company="Henrik Jensen">
// Copyright 2025 Henrik Jensen
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace Hj.ReverseProxy.Certificate.Strategy;

/// <summary>
/// Default implementation for creating certificates using standard .NET APIs.
/// </summary>
internal sealed class DefaultCertificateCreator(IEnumerable<ICertificateStrategy> strategies) : ICertificateCreator
{
  /// <inheritdoc/>
  public X509Certificate2 CreateSelfSignedCa(
    AsymmetricAlgorithm key,
    CertificateRequest request,
    DateTimeOffset validFrom,
    DateTimeOffset validTo)
  {
    return request.CreateSelfSigned(validFrom, validTo);
  }

  /// <inheritdoc/>
  public byte[] CreateSignedCertificate(
    AsymmetricAlgorithm key,
    CertificateRequest request,
    X500DistinguishedName issuerName,
    X509SignatureGenerator caSignatureGenerator,
    DateTimeOffset validFrom,
    DateTimeOffset validTo,
    byte[] serialNumber)
  {
    using var certificate = request.Create(
      issuerName,
      caSignatureGenerator,
      validFrom,
      validTo,
      serialNumber);

    var strategy = GetStrategy(key);
    using var certificateWithKey = strategy.CopyWithPrivateKey(certificate, key);
    return certificateWithKey.Export(X509ContentType.Pkcs12);
  }

  private ICertificateStrategy GetStrategy(AsymmetricAlgorithm key)
  {
    var keyType = key.GetType();
    return strategies.FirstOrDefault(x => x.CanHandle(keyType)) ?? throw new NotSupportedException($"Algorithm type '{keyType.Name}' is not supported");
  }
}
