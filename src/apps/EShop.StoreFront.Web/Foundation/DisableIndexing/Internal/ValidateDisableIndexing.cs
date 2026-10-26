// <copyright file="ValidateDisableIndexing.cs" company="Henrik Jensen">
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

using EPiServer.Security;
using EPiServer.Validation;

namespace Hj.EShop.StoreFront.Web.Foundation.DisableIndexing.Internal;

internal sealed class ValidateDisableIndexing : IContentSaveValidate<IDisableIndexing>
{
    private const string ErrorMessage = "Cannot change 'disable indexing' when inheriting access rights from parent content.";

    public ContentValidationSkipOption SkipOption => ContentValidationSkipOption.None;

    public IEnumerable<ValidationError> Validate(IDisableIndexing instance, ContentSaveValidationContext context)
    {
        return Validate(instance);
    }

    public IEnumerable<ValidationError> Validate(IDisableIndexing instance)
    {
        if (instance is IContentSecurable contentSecurable)
        {
            IContentSecurityDescriptor csd = contentSecurable.GetContentSecurityDescriptor();
            if (csd.IsInherited && instance.DisableIndexing == true)
            {
                yield return new ValidationError()
                {
                    Source = this,
                    PropertyName = nameof(IDisableIndexing.DisableIndexing),
                    ValidationType = ValidationErrorType.PropertyValidation,
                    Severity = ValidationErrorSeverity.Error,
                    ErrorMessage = ErrorMessage,
                };
            }
        }
    }
}
