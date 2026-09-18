// <copyright file="ICaLoader.cs" company="Henrik Jensen">
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
/// Defines a strategy for loading CA certificates from PEM files.
/// </summary>
internal interface ICaLoader
{
  /// <summary>
  /// Loads a CA certificate from PEM-encoded certificate and key content.
  /// </summary>
  /// <param name="certContents">The PEM-encoded certificate content.</param>
  /// <param name="keyContents">The PEM-encoded private key content.</param>
  /// <returns>An <see cref="X509Certificate2"/> instance with the private key attached.</returns>
  X509Certificate2 LoadFromPem(string certContents, string keyContents);
}
