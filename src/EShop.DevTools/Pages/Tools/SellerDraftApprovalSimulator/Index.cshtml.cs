// <copyright file="Index.cshtml.cs" company="Henrik Jensen">
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

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Hj.EShop.Common;
using Hj.EShop.DevTools.Hubs;
using Hj.EShop.DevTools.Services;
using Hj.EShop.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace Hj.EShop.DevTools.Pages.Tools.SellerDraftApprovalSimulator;

internal sealed class IndexModel(
    SellerPendingSubmissionStore store,
    ServiceBusClient serviceBusClient,
    BlobContainerClient submissionsImageContainer,
    IHubContext<SellerSubmissionsHub> hubContext) : PageModel
{
    public IReadOnlyCollection<SellerPendingSubmission> PendingSubmissions { get; private set; } = [];

    public void OnGet()
    {
        PendingSubmissions = store.GetAll();
    }

    // Only serves a blobReference belonging to a currently pending submission, so this
    // handler can't be used to read arbitrary blobs out of the container.
    public async Task<IActionResult> OnGetImageAsync(string blobReference, CancellationToken cancellationToken)
    {
        bool isKnownImage = store.GetAll()
            .Any(submission => submission.Message.ImageBlobReferences.Contains(blobReference));
        if (!isKnownImage)
        {
            return NotFound();
        }

        BlobDownloadStreamingResult download = await submissionsImageContainer
            .GetBlobClient(blobReference)
            .DownloadStreamingAsync(cancellationToken: cancellationToken);

        return File(download.Content, download.Details.ContentType);
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        if (!store.TryGet(submissionId, out SellerPendingSubmission? submission))
        {
            return RedirectToPage();
        }

        // Not a production SKU-generation algorithm. Uses UUIDv7's trailing (random) hex
        // digits, not its leading (timestamp) ones, to avoid collisions between SKUs
        // generated in the same instant.
        string sku = $"SKU-{Guid.CreateVersion7().ToString("N")[^8..].ToUpperInvariant()}";
        SubmissionResultMessage message = new(submission.Message.SubmissionId, Approved: true, AssignedSku: sku, RejectionReason: null);
        await PublishResultAsync(message, cancellationToken);
        store.Remove(submissionId);
        await hubContext.Clients.All.SendAsync(SellerSubmissionsHub.SubmissionsChangedEvent, cancellationToken);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid submissionId, string rejectionReason, CancellationToken cancellationToken)
    {
        if (!store.TryGet(submissionId, out SellerPendingSubmission? submission))
        {
            return RedirectToPage();
        }

        SubmissionResultMessage message = new(submission.Message.SubmissionId, Approved: false, AssignedSku: null, RejectionReason: rejectionReason);
        await PublishResultAsync(message, cancellationToken);
        store.Remove(submissionId);
        await hubContext.Clients.All.SendAsync(SellerSubmissionsHub.SubmissionsChangedEvent, cancellationToken);

        return RedirectToPage();
    }

    private async Task PublishResultAsync(SubmissionResultMessage message, CancellationToken cancellationToken)
    {
        await using ServiceBusSender sender = serviceBusClient.CreateSender(KnownNames.ResourceSellerSubmissionsResult);
        await sender.SendMessagesAsync([new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(message))], cancellationToken);
    }
}
