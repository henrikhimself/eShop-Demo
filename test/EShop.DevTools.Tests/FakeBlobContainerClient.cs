// <copyright file="FakeBlobContainerClient.cs" company="Henrik Jensen">
// Copyright 2026 Henrik Jensen
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

using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Hj.EShop.DevTools.Tests;

// In-memory BlobContainerClient/BlobClient test double for
// SellerDraftApprovalSimulatorPageTests's image handler - avoids needing an Azurite
// emulator for these tests. Mirrors EShop.SellerPortal.Bff.Tests's own
// FakeBlobContainerClient (a separate copy, not shared, since the two projects don't
// reference each other).
internal sealed class FakeBlobContainerClient : BlobContainerClient
{
    public Dictionary<string, (byte[] Content, string ContentType)> Blobs { get; } = [];

    public override BlobClient GetBlobClient(string blobName)
    {
        return new FakeBlobClient(blobName, this);
    }
}

internal sealed class FakeBlobClient(string name, FakeBlobContainerClient owner) : BlobClient
{
    public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(
        BlobDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        (byte[] Content, string ContentType) blob = owner.Blobs[name];
        BlobDownloadStreamingResult result = BlobsModelFactory.BlobDownloadStreamingResult(
            new MemoryStream(blob.Content), BlobsModelFactory.BlobDownloadDetails(contentType: blob.ContentType));

        return Task.FromResult(Response.FromValue(result, (Response)new NoOpResponse()));
    }
}

// Minimal Response stand-in: only Dispose is ever called by this file's own
// Response.FromValue wrapper, none of the other members are exercised.
[SuppressMessage("Design", "CA1063", Justification = "Trivial test double; no unmanaged resources to release.")]
internal sealed class NoOpResponse : Response
{
    public override int Status => 200;

    public override string ReasonPhrase => "OK";

    public override Stream? ContentStream { get; set; }

    public override string ClientRequestId { get; set; } = string.Empty;

    public override void Dispose()
    {
    }

    protected override bool ContainsHeader(string name)
    {
        return false;
    }

    protected override IEnumerable<HttpHeader> EnumerateHeaders()
    {
        return [];
    }

    protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
    {
        value = null;
        return false;
    }

    protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
    {
        values = null;
        return false;
    }
}
