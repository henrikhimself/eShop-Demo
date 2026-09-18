// <copyright file="ICertificateStrategy.cs" company="Henrik Jensen">
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

internal interface ICertificateStrategy
{
  bool CanHandle(Type asymmetricAlgorithm);

  /// <summary>
  /// A public key OID is an object identifier identifying the algorithm.
  /// <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-gpnap/ff1a8675-0008-408c-ba5f-686a10389adc">See more</see>.
  /// </summary>
  /// <param name="publicKeyAlgOid">The algorithm OID.</param>
  /// <returns>True if strategy matches the provided OID.</returns>
  bool CanHandle(string publicKeyAlgOid);

  AsymmetricAlgorithm CreateKey();

  CertificateRequest CreateCertificateRequest(AsymmetricAlgorithm key, X500DistinguishedName distinguishedName);

  X509SignatureGenerator GetSignatureGenerator(X509Certificate2 certificate);

  X509SignatureGenerator GetSignatureGenerator(AsymmetricAlgorithm key);

  X509Certificate2 CopyWithPrivateKey(X509Certificate2 certificate, AsymmetricAlgorithm key);

  string ExportPrivateKeyPem(X509Certificate2 certificate);

  string ExportPrivateKeyPem(AsymmetricAlgorithm key);
}
