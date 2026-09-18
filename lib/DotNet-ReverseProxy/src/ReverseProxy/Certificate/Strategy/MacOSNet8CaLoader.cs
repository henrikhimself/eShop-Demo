// <copyright file="MacOSNet8CaLoader.cs" company="Henrik Jensen">
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
#if NET8_0
using System.Security.Cryptography.Pkcs;

namespace Hj.ReverseProxy.Certificate.Strategy;

/// <summary>
/// macOS .NET 8 implementation for loading CA certificates using Pkcs12Builder to avoid keychain issues.
/// </summary>
internal sealed class MacOSNet8CaLoader : ICaLoader
{
  /// <inheritdoc/>
  public X509Certificate2 LoadFromPem(string certContents, string keyContents)
  {
    using var certificate = X509Certificate2.CreateFromPem(certContents);
    AsymmetricAlgorithm key;

    // Try ECDsa first, then fall back to RSA
    try
    {
      key = ECDsa.Create();
      key.ImportFromPem(keyContents);
    }
    catch
    {
      key = RSA.Create();
      key.ImportFromPem(keyContents);
    }

    using (key)
    {
      var safeContents = new Pkcs12SafeContents();
      safeContents.AddCertificate(certificate);
      safeContents.AddShroudedKey(key, string.Empty, new PbeParameters(
        PbeEncryptionAlgorithm.Aes256Cbc,
        HashAlgorithmName.SHA256,
        2048));
      var builder = new Pkcs12Builder();
      builder.AddSafeContentsUnencrypted(safeContents);
      builder.SealWithMac(string.Empty, HashAlgorithmName.SHA256, 2048);
      var pfxBytes = builder.Encode();

#pragma warning disable SYSLIB0057 // Type or member is obsolete
      var ca = new X509Certificate2(pfxBytes, string.Empty, X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057 // Type or member is obsolete
      return ca;
    }
  }
}
#endif
