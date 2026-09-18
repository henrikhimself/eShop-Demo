// <copyright file="CertificateEcdsa.cs" company="Henrik Jensen">
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

internal sealed class CertificateEcdsa : ICertificateStrategy
{
  public bool CanHandle(Type asymmetricAlgorithm) => typeof(ECDsa).IsAssignableFrom(asymmetricAlgorithm);

  public bool CanHandle(string publicKeyAlgOid) => publicKeyAlgOid == CertificateConstants.EcdsaOid;

  public AsymmetricAlgorithm CreateKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

  public CertificateRequest CreateCertificateRequest(AsymmetricAlgorithm key, X500DistinguishedName distinguishedName)
    => new(distinguishedName, (ECDsa)key, HashAlgorithmName.SHA256);

  public X509SignatureGenerator GetSignatureGenerator(X509Certificate2 certificate)
    => X509SignatureGenerator.CreateForECDsa(GetKey(certificate));

  public X509SignatureGenerator GetSignatureGenerator(AsymmetricAlgorithm key)
    => X509SignatureGenerator.CreateForECDsa((ECDsa)key);

  public X509Certificate2 CopyWithPrivateKey(X509Certificate2 certificate, AsymmetricAlgorithm key)
    => certificate.CopyWithPrivateKey((ECDsa)key);

  public string ExportPrivateKeyPem(X509Certificate2 certificate) => GetKey(certificate).ExportECPrivateKeyPem();

  public string ExportPrivateKeyPem(AsymmetricAlgorithm key) => ((ECDsa)key).ExportECPrivateKeyPem();

  private static ECDsa GetKey(X509Certificate2 certificate)
    => certificate.GetECDsaPrivateKey() ?? throw new InvalidOperationException("Certificate has no private key");
}
