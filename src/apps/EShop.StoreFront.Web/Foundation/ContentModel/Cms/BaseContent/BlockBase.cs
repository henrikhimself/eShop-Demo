// <copyright file="BlockBase.cs" company="Henrik Jensen">
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

using System.ComponentModel;
using System.Reflection;
using Castle.Core.Internal;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.BaseContent;

public abstract class BlockBase : BlockData
{
    public override void SetDefaultValues(ContentType contentType)
    {
        base.SetDefaultValues(contentType);

        Type? baseType = GetType().BaseType;
        if (baseType == null)
        {
            base.SetDefaultValues(contentType);
            return;
        }

        PropertyInfo[] properties = baseType.GetProperties();
        foreach (PropertyInfo property in properties)
        {
            DefaultValueAttribute defaultValueAttribute = property.GetAttribute<DefaultValueAttribute>();
            if (defaultValueAttribute != null)
            {
                this[property.Name] = defaultValueAttribute.Value;
            }
        }
    }
}
