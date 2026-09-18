// <copyright file="ICertificateCreator.cs" company="Henrik Jensen">
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

namespace Hj.ReverseProxy.Certificate;

/// <summary>
/// Defines a strategy for creating X.509 certificates.
/// </summary>
internal interface ICertificateCreator
{
  /// <summary>
  /// Creates a self-signed CA certificate.
  /// </summary>
  /// <param name="key">The asymmetric key to use for the certificate.</param>
  /// <param name="request">The certificate request containing subject, extensions, etc.</param>
  /// <param name="validFrom">The date and time when the certificate becomes valid.</param>
  /// <param name="validTo">The date and time when the certificate expires.</param>
  /// <returns>A self-signed CA certificate with the private key attached.</returns>
  X509Certificate2 CreateSelfSignedCa(
    AsymmetricAlgorithm key,
    CertificateRequest request,
    DateTimeOffset validFrom,
    DateTimeOffset validTo);

  /// <summary>
  /// Creates a signed certificate using a CA certificate.
  /// </summary>
  /// <param name="key">The asymmetric key to use for the certificate.</param>
  /// <param name="request">The certificate request containing subject, extensions, etc.</param>
  /// <param name="issuerName">The issuer name from the CA certificate.</param>
  /// <param name="caSignatureGenerator">The signature generator from the CA certificate.</param>
  /// <param name="validFrom">The date and time when the certificate becomes valid.</param>
  /// <param name="validTo">The date and time when the certificate expires.</param>
  /// <param name="serialNumber">The serial number for the certificate.</param>
  /// <returns>A byte array containing the certificate in PKCS#12 format.</returns>
  byte[] CreateSignedCertificate(
    AsymmetricAlgorithm key,
    CertificateRequest request,
    X500DistinguishedName issuerName,
    X509SignatureGenerator caSignatureGenerator,
    DateTimeOffset validFrom,
    DateTimeOffset validTo,
    byte[] serialNumber);
}
