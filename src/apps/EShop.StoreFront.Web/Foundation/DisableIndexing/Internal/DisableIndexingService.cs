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
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Security;

namespace Hj.EShop.StoreFront.Web.Foundation.DisableIndexing.Internal;

internal sealed class DisableIndexingService : IDisableIndexingService
{
    private readonly IContentEvents _contentEvents;
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentSecurityRepository _contentSecurityRepository;
    private readonly IContentVersionRepository _contentVersionRepository;
    private readonly ContentRootRepository _contentRootRepository;

    public DisableIndexingService(
        IContentEvents contentEvents,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        IContentSecurityRepository contentSecurityRepository,
        IContentVersionRepository contentVersionRepository,
        ContentRootRepository contentRootRepository)
    {
        _contentEvents = contentEvents;
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
        _contentSecurityRepository = contentSecurityRepository;
        _contentVersionRepository = contentVersionRepository;
        _contentRootRepository = contentRootRepository;
    }

    public void Initialize()
    {
        _contentEvents.CreatingContent += OnCreatingContent;
        _contentEvents.CreatedContent += OnCreatedContent;
        _contentEvents.PublishedContent += OnPublishedContent;

        EnsureNotIndexedFolder(_contentRootRepository.Load(SystemContentRootNames.GlobalAssets));
    }

    public void Uninitialize()
    {
        _contentEvents.PublishedContent -= OnPublishedContent;
        _contentEvents.CreatedContent -= OnCreatedContent;
        _contentEvents.CreatingContent -= OnCreatingContent;
    }

    public void OnCreatingContent(object? sender, ContentEventArgs e)
    {
        if (IgnoreContent(e))
        {
            return;
        }

        SetDisableIndexValue(e.Content, true);
    }

    public void OnCreatedContent(object? sender, ContentEventArgs e)
    {
        if (IgnoreContent(e))
        {
            return;
        }

        var contentSecurable = (IContentSecurable)e.Content;
        var csd = (IContentSecurityDescriptor)contentSecurable.GetContentSecurityDescriptor().CreateWritableClone();
        csd.ToLocal(true);
        DisableIndexing(csd);
    }

    public void OnPublishedContent(object? sender, ContentEventArgs e)
    {
        if (IgnoreContent(e))
        {
            return;
        }

        IContent newContent = e.Content;
        bool? disableIndex = GetDisableIndexValue(newContent);
        if (disableIndex is null)
        {
            return;
        }

        bool? previousDisableIndex = GetPreviousDisableIndexValue(e);
        if (previousDisableIndex is not null && previousDisableIndex == disableIndex)
        {
            return;
        }

        var contentSecurable = (IContentSecurable)newContent;
        IContentSecurityDescriptor csd = contentSecurable.GetContentSecurityDescriptor();
        if (csd.IsInherited)
        {
            return;
        }

        if (disableIndex.Value)
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
        IContentSecurityDescriptor writable = contentSecurityDescriptor.IsReadOnly
            ? (IContentSecurityDescriptor)contentSecurityDescriptor.CreateWritableClone()
            : contentSecurityDescriptor;

        AccessControlEntry searchIndexerEntry = new(SearchIndexerRole.RoleName, AccessLevel.NoAccess, SecurityEntityType.Role);
        writable.AddEntry(searchIndexerEntry);

        _contentSecurityRepository.Save(writable.ContentLink, writable, SecuritySaveType.Replace);
    }

    public void EnableIndexing(IContentSecurityDescriptor contentSecurityDescriptor)
    {
        IContentSecurityDescriptor writable = contentSecurityDescriptor.IsReadOnly
            ? (IContentSecurityDescriptor)contentSecurityDescriptor.CreateWritableClone()
            : contentSecurityDescriptor;

        AccessControlEntry searchIndexerEntry = new(SearchIndexerRole.RoleName, AccessLevel.Read | AccessLevel.Edit, SecurityEntityType.Role);
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

    public bool? GetPreviousDisableIndexValue(ContentEventArgs e)
    {
        ContentVersion? previousVersion = _contentVersionRepository.ListPublished(e.ContentLink)
            .OrderDescending()
            .Skip(1)
            .FirstOrDefault();

        if (previousVersion is not null)
        {
            IContent previousContent = _contentRepository.Get<IContent>(previousVersion.ContentLink);
            return GetDisableIndexValue(previousContent);
        }

        return null;
    }

    public bool IgnoreContent(ContentEventArgs e)
    {
        IContent content = e.Content;

        if (content is not IContentSecurable)
        {
            return true;
        }

        ContentType contractType = _contentTypeRepository.Load<IDisableIndexing>();
        ContentType contentType = _contentTypeRepository.Load(content.ContentTypeID);
        bool hasContract = contentType.Contracts.Any(x => x.GUID == contractType.GUID);
        if (hasContract)
        {
            return false;
        }

        return true;
    }

    private static bool? GetDisableIndexValue(IContentData content)
    {
        bool? value = content.Property[nameof(IDisableIndexing.DisableIndexing)].Value as bool?;
        return value;
    }

    private static void SetDisableIndexValue(IContentData content, bool? value)
    {
        content.Property[nameof(IDisableIndexing.DisableIndexing)].Value = value;
    }
}
