// <copyright file="CertificateApp.cs" company="Henrik Jensen">
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
using Microsoft.Extensions.Caching.Memory;

namespace Hj.ReverseProxy.Certificate;

internal sealed class CertificateApp(
  ILogger<CertificateApp> logger,
  ICertificateConfig certificateConfig,
  IMemoryCache memoryCache,
  CertificateStore certificateStore,
  CertificateFactory certificateFactory)
{
  public X509Certificate2 GetCertificate(string dnsName)
  {
    var certificate = memoryCache.GetOrCreate(dnsName, entry =>
    {
      var selfSignedOptions = certificateConfig.GetOptions();

      using var ca = GetOrCreateCa(selfSignedOptions);
      using var key = certificateFactory.CreateKey(selfSignedOptions.AlgorithmOid);

      var isWildcard = dnsName.StartsWith('*');
      var cn = isWildcard
        ? dnsName[2..]
        : dnsName;

      if (logger.IsEnabled(LogLevel.Information))
      {
        logger.LogInformation("Missing certificate, dns name '{DnsName}', is wildcard '{IsWildcard}'", dnsName, isWildcard);
      }

      return certificateFactory.CreateCertificate(key, ca, $"CN={cn}", san =>
      {
        san.AddIpAddress(IPAddress.Loopback);

        san.AddDnsName(cn);
        if (isWildcard)
        {
          san.AddDnsName("*." + dnsName);
        }
      });
    });

    return certificate!;
  }

  private X509Certificate2 GetOrCreateCa(SelfSignedOptions selfSignedOptions)
  {
    var ca = certificateStore.LoadCa(selfSignedOptions);
    if (ca is null)
    {
      using var key = certificateFactory.CreateKey(selfSignedOptions.AlgorithmOid);
      ca = certificateFactory.CreateCa(key, selfSignedOptions.SubjectName);
      certificateStore.SaveCa(selfSignedOptions, ca, key);
    }

    return ca;
  }
}
