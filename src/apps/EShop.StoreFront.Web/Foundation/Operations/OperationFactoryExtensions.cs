// <copyright file="OperationFactoryExtensions.cs" company="Henrik Jensen">
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

using System.Diagnostics;
using System.Globalization;
using EPiServer.Framework.Initialization;
using EPiServer.Web.Routing;
using Hj.EShop.StoreFront.Web.Foundation.Operations.Internal;
using Microsoft.AspNetCore.Mvc;

namespace Hj.EShop.StoreFront.Web.Foundation.Operations;

internal static class OperationFactoryExtensions
{
    [DebuggerStepThrough]
    public static OperationDataRequest<T> CreateOperationRequest<T>(this OperationRequest request, T data)
    {
        return new OperationDataRequest<T>()
        {
            CancellationToken = request.CancellationToken,
            Context = request.Context,
            Data = data,
        };
    }

    [DebuggerStepThrough]
    public static OperationRequest CreateOperationRequest(this Controller controller)
    {
        return controller.ControllerContext.HttpContext.CreateOperationRequest();
    }

    [DebuggerStepThrough]
    public static OperationDataRequest<T> CreateOperationRequest<T>(this Controller controller, T data)
    {
        return controller.ControllerContext.HttpContext.CreateOperationRequest(data);
    }

    [DebuggerStepThrough]
    public static OperationRequest CreateOperationRequest(this InitializationEngine initializationEngine)
    {
        return new OperationRequest()
        {
            CancellationToken = CancellationToken.None,
            Context = ToOperationContext(initializationEngine),
        };
    }

    [DebuggerStepThrough]
    public static OperationDataRequest<T> CreateOperationRequest<T>(this InitializationEngine initializationEngine, T data)
    {
        return new OperationDataRequest<T>()
        {
            CancellationToken = CancellationToken.None,
            Context = ToOperationContext(initializationEngine),
            Data = data,
        };
    }

    [DebuggerStepThrough]
    public static OperationRequest CreateOperationRequest(this HttpContext httpContext)
    {
        return new OperationRequest()
        {
            CancellationToken = httpContext.RequestAborted,
            Context = new()
            {
                Principal = httpContext.User,
                ContentLink = httpContext.GetContentLink(),
                Language = ToCultureInfo(httpContext.GetRequestedLanguage()),
            },
        };
    }

    [DebuggerStepThrough]
    public static OperationDataRequest<T> CreateOperationRequest<T>(this HttpContext httpContext, T data)
    {
        return new OperationDataRequest<T>()
        {
            CancellationToken = httpContext.RequestAborted,
            Context = new()
            {
                Principal = httpContext.User,
                ContentLink = httpContext.GetContentLink(),
                Language = ToCultureInfo(httpContext.GetRequestedLanguage()),
            },
            Data = data,
        };
    }

    private static OperationContext ToOperationContext(InitializationEngine initializationEngine)
    {
        using IServiceScope scope = initializationEngine.Services.CreateScope();
        HttpContext? httpContext = scope
            .ServiceProvider
            .GetService<IHttpContextAccessor>()?.HttpContext;

        return ToOperationContext(httpContext);
    }

    private static OperationContext ToOperationContext(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return new();
        }

        return new()
        {
            Principal = httpContext.User,
            ContentLink = httpContext.GetContentLink(),
            Language = ToCultureInfo(httpContext.GetRequestedLanguage()),
        };
    }

    private static CultureInfo? ToCultureInfo(string? language)
    {
        return string.IsNullOrWhiteSpace(language)
            ? null
            : CultureInfo.GetCultureInfo(language);
    }
}
