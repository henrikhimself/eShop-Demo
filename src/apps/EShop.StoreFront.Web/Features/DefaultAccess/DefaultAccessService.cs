// <copyright file="DefaultAccessService.cs" company="Henrik Jensen">
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
using EPiServer.Authorization;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Security;
using Hj.EShop.StoreFront.Web.Foundation.Operations;

namespace Hj.EShop.StoreFront.Web.Features.DefaultAccess;

internal sealed class DefaultAccessService : IDefaultAccessService
{
    private const AccessLevel AdminAccess = AccessLevel.FullAccess;
    private const AccessLevel EditorAccess = AccessLevel.Create | AccessLevel.Read | AccessLevel.Edit | AccessLevel.Delete | AccessLevel.Publish;
    private const AccessLevel EveryoneAccess = AccessLevel.Read;
    private const AccessLevel SearchIndexerAccess = AccessLevel.Read | AccessLevel.Edit;

    private readonly IContentLoader _contentLoader;
    private readonly IContentSecurityRepository _contentSecurityRepository;
    private readonly ContentRootRepository _contentRootRepository;

    public DefaultAccessService(
        IContentLoader contentLoader,
        IContentSecurityRepository contentSecurityRepository,
        ContentRootRepository contentRootRepository)
    {
        _contentLoader = contentLoader;
        _contentSecurityRepository = contentSecurityRepository;
        _contentRootRepository = contentRootRepository;
    }

    public void ResetRootPage()
    {
        ContentReference rootReference = _contentRootRepository.Load(SystemContentRootNames.RootPage);
        Reset(rootReference, SecuritySaveType.Replace, acl =>
        {
            acl.Clear();
            acl.AddEntry(new(Roles.CmsAdmins, AdminAccess));
            acl.AddEntry(new(Roles.CmsEditors, EditorAccess));
            acl.AddEntry(new(EveryoneRoleName, EveryoneAccess));
            acl.AddEntry(new(SearchIndexerRoleName, SearchIndexerAccess));
        });
    }

    public void ResetBluePrints()
    {
        ContentReference rootReference = _contentRootRepository.Load(SystemContentRootNames.Blueprints);
        Reset(rootReference, SecuritySaveType.Replace, acl =>
        {
            acl.Clear();
            acl.IsInherited = false;
            acl.AddEntry(new(Roles.CmsAdmins, AdminAccess));
            acl.AddEntry(new(Roles.CmsEditors, EditorAccess));
            acl.AddEntry(new(SearchIndexerRoleName, SearchIndexerAccess));
        });
    }

    public void ResetWasteBasket()
    {
        ContentReference rootReference = _contentRootRepository.Load(SystemContentRootNames.WasteBasket);
        Reset(rootReference, SecuritySaveType.Replace, acl =>
        {
            acl.Clear();
            acl.IsInherited = false;
            acl.AddEntry(new(Roles.CmsAdmins, AdminAccess));
            acl.AddEntry(new(Roles.CmsEditors, EditorAccess));
        });
    }

    public void Reset(OperationDataRequest<DefaultAccessData> request)
    {
        DefaultAccessData data = request.Data;
        Reset(data.ContentReference, data.SaveType, data.Action);
    }

    private static string EveryoneRoleName => EveryoneRole.RoleName;

    private static string SearchIndexerRoleName => SearchIndexerRole.RoleName;

    private void Reset(ContentReference contentReference, SecuritySaveType securitySaveType, Action<IContentSecurityDescriptor> action)
    {
        var content = (IContentSecurable)_contentLoader.Get<IContent>(contentReference);
        var acl = (IContentSecurityDescriptor)content.GetContentSecurityDescriptor().CreateWritableClone();
        action(acl);
        _contentSecurityRepository.Save(acl.ContentLink, acl, securitySaveType);
    }
}
