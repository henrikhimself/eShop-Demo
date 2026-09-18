// <copyright file="CertificateConfig.cs" company="Henrik Jensen">
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

using Hj.ReverseProxy.Certificate.Models;

namespace Hj.ReverseProxy.Certificate;

internal sealed class CertificateConfig(
  IConfiguration configuration) : ICertificateConfig
{
  public SelfSignedOptions GetOptions()
  {
    var selfSignedOptions = configuration.GetSection("SelfSignedCertificate").Get<SelfSignedOptions>()
      ?? throw new InvalidOperationException("Configuration is missing");

    if (string.IsNullOrWhiteSpace(selfSignedOptions.CaFilePath))
    {
      throw new InvalidOperationException("CA file path is not configured");
    }

    if (selfSignedOptions.CaFilePath.Contains("{REVERSEPROXY_HOME}", StringComparison.Ordinal))
    {
      var reverseProxyHome = Environment.GetEnvironmentVariable("REVERSEPROXY_HOME");
      if (string.IsNullOrWhiteSpace(reverseProxyHome))
      {
        throw new InvalidOperationException("Environment variable REVERSEPROXY_HOME is not set");
      }

      selfSignedOptions.CaFilePath = selfSignedOptions.CaFilePath.Replace("{REVERSEPROXY_HOME}", reverseProxyHome, StringComparison.Ordinal);
    }

    if (string.IsNullOrWhiteSpace(selfSignedOptions.CaName))
    {
      selfSignedOptions.CaName = CertificateConstants.DefaultCaName;
    }

    if (string.IsNullOrWhiteSpace(selfSignedOptions.AlgorithmOid))
    {
      selfSignedOptions.AlgorithmOid = CertificateConstants.EcdsaOid;
    }

    if (string.IsNullOrWhiteSpace(selfSignedOptions.SubjectName))
    {
      selfSignedOptions.SubjectName = CertificateConstants.DefaultCaSubjectName;
    }

    return selfSignedOptions;
  }
}
