// <copyright file="DisableIndexingService.cs" company="Henrik Jensen">
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

using EPiServer;
using EPiServer.Security;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using Hj.EShop.StoreFront.Web.Foundation.DisableIndexing.Contracts;
using EPiServer.DataAccess;

namespace Hj.EShop.StoreFront.Web.Foundation.DisableIndexing.Internal;

internal sealed class DisableIndexingService : IDisableIndexingService
{
    private readonly IContentEvents _contentEvents;
    private readonly IContentRepository _contentRepository;
    private readonly IContentSecurityRepository _contentSecurityRepository;
    private readonly IContentVersionRepository _contentVersionRepository;
    private readonly ContentRootRepository _contentRootRepository;

    public DisableIndexingService(
        IContentEvents contentEvents,
        IContentRepository contentRepository,
        IContentSecurityRepository contentSecurityRepository,
        IContentVersionRepository contentVersionRepository,
        ContentRootRepository contentRootRepository)
    {
        _contentEvents = contentEvents;
        _contentRepository = contentRepository;
        _contentSecurityRepository = contentSecurityRepository;
        _contentVersionRepository = contentVersionRepository;
        _contentRootRepository = contentRootRepository;
    }

    public void Initialize()
    {
        _contentEvents.PublishedContent += OnPublishedContent;

        EnsureNotIndexedFolder(_contentRootRepository.Load(SystemContentRootNames.GlobalAssets));
    }

    public void Uninitialize()
    {
        _contentEvents.PublishedContent -= OnPublishedContent;
    }

    public void OnPublishedContent(object? sender, ContentEventArgs e)
    {
        if (e.Content is not IDisableIndexing newContent
            || newContent is not IContentSecurable contentSecurable
            || newContent.DisableIndexing is null)
        {
            return;
        }

        IDisableIndexing? previousContent = GetPreviousVersion(e.ContentLink);
        if (previousContent is not null && previousContent.DisableIndexing == newContent.DisableIndexing)
        {
            return;
        }

        IContentSecurityDescriptor csd = contentSecurable.GetContentSecurityDescriptor();
        if (csd.IsInherited)
        {
            return;
        }

        if (newContent.DisableIndexing.Value)
        {
            DisableIndexing(csd);
        }
        else
        {
            EnableIndexing(csd);
        }
    }

    public void DisableIndexing(IContentSecurityDescriptor contentSecurityDescriptor)
    {
        AccessControlEntry searchIndexerEntry = new(SearchIndexerRole.RoleName, AccessLevel.NoAccess, SecurityEntityType.Role);

        var writable = (IContentSecurityDescriptor)contentSecurityDescriptor.CreateWritableClone();
        writable.AddEntry(searchIndexerEntry);

        _contentSecurityRepository.Save(writable.ContentLink, writable, SecuritySaveType.Replace);
    }

    public void EnableIndexing(IContentSecurityDescriptor contentSecurityDescriptor)
    {
        AccessControlEntry searchIndexerEntry = new(SearchIndexerRole.RoleName, AccessLevel.Read | AccessLevel.Edit, SecurityEntityType.Role);

        var writable = (IContentSecurityDescriptor)contentSecurityDescriptor.CreateWritableClone();
        writable.AddEntry(searchIndexerEntry);

        _contentSecurityRepository.Save(writable.ContentLink, writable, SecuritySaveType.Replace);
    }

    public void EnsureNotIndexedFolder(ContentReference parentLink)
    {
        const string FolderName = "[Not Indexed]";

        if (_contentRepository.GetChildren<ContentAssetFolder>(parentLink).Any(x => x.Name == FolderName))
        {
            return;
        }

        ContentAssetFolder folder = _contentRepository.GetDefault<ContentAssetFolder>(parentLink);
        folder.Name = FolderName;
        ContentReference folderReference = _contentRepository.Save(folder, SaveAction.Publish, AccessLevel.NoAccess);

        folder = _contentRepository.Get<ContentAssetFolder>(folderReference);
        var csd = (IContentSecurityDescriptor)folder.GetContentSecurityDescriptor().CreateWritableClone();
        csd.ToLocal(true);

        DisableIndexing(csd);
    }

    public IDisableIndexing? GetPreviousVersion(ContentReference contentReference)
    {
        ContentVersion? previousVersion = _contentVersionRepository.ListPublished(contentReference)
            .OrderDescending()
            .Skip(1)
            .FirstOrDefault();

        if (previousVersion is not null)
        {
            IDisableIndexing previousContent = _contentRepository.Get<IDisableIndexing>(previousVersion.ContentLink);
            return previousContent;
        }

        return null;
    }
}
