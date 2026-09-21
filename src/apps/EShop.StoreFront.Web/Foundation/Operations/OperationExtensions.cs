// <copyright file="OperationExtensions.cs" company="Henrik Jensen">
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
using System.Security.Claims;
using EPiServer.Web.Routing;
using Hj.EShop.StoreFront.Web.Foundation.Operations.Internal;
using Microsoft.AspNetCore.Mvc;

namespace Hj.EShop.StoreFront.Web.Foundation.Operations;

internal static class OperationExtensions
{
    extension(OperationContext context)
    {
        public ClaimsPrincipal? ClaimsPrincipal => context.Principal as ClaimsPrincipal;

        public ClaimsIdentity? ClaimsIdentity => context.ClaimsPrincipal?.Identity as ClaimsIdentity;

        public bool IsUserAuthenticated => context.ClaimsIdentity?.IsAuthenticated ?? false;
    }

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

    [DebuggerStepThrough]
    public static Task<OperationResponse> OkTask(this OperationRequest request)
    {
        return Task.FromResult(request.Ok());
    }

    [DebuggerStepThrough]
    public static Task<OperationDataResponse<T>> OkTask<T>(this OperationRequest request, T? data = default)
    {
        return Task.FromResult(request.Ok(data));
    }

    [DebuggerStepThrough]
    public static OperationResponse Ok(this OperationRequest request)
    {
        return new OperationResponse()
        {
            IsSuccess = true,
            Context = request.Context,
        };
    }

    [DebuggerStepThrough]
    public static OperationDataResponse<T> Ok<T>(this OperationRequest request, T? data = default)
    {
        return new OperationDataResponse<T>()
        {
            IsSuccess = true,
            Context = request.Context,
            Data = data,
            HasData = data is not null,
        };
    }

    [DebuggerStepThrough]
    public static Task<OperationResponse> FailTask(this OperationRequest request, string message)
    {
        return Task.FromResult(request.Fail(message));
    }

    [DebuggerStepThrough]
    public static Task<OperationDataResponse<T>> FailTask<T>(this OperationRequest request, string message, T? data = default)
    {
        return Task.FromResult(request.Fail(message, data));
    }

    [DebuggerStepThrough]
    public static Task<OperationResponse> FailTask(this OperationRequest request, Exception exception)
    {
        return Task.FromResult(request.Fail(exception));
    }

    [DebuggerStepThrough]
    public static Task<OperationDataResponse<T>> FailTask<T>(this OperationRequest request, Exception exception, T? data = default)
    {
        return Task.FromResult(request.Fail(exception, data));
    }

    [DebuggerStepThrough]
    public static OperationResponse Fail(this OperationRequest request, string message)
    {
        return request.Fail(new InvalidOperationException(message));
    }

    [DebuggerStepThrough]
    public static OperationDataResponse<T> Fail<T>(this OperationRequest request, string message, T? data = default)
    {
        return request.Fail(new InvalidOperationException(message), data);
    }

    [DebuggerStepThrough]
    public static OperationResponse Fail(this OperationRequest request, Exception exception)
    {
        return new OperationResponse()
        {
            Error = exception is InvalidOperationException invalidOperation
                ? invalidOperation
                : new InvalidOperationException("Operation failed", exception),
            Context = request.Context,
        };
    }

    [DebuggerStepThrough]
    public static OperationDataResponse<T> Fail<T>(this OperationRequest request, Exception exception, T? data = default)
    {
        return new OperationDataResponse<T>()
        {
            Error = exception is InvalidOperationException invalidOperation
                ? invalidOperation
                : new InvalidOperationException("Operation failed", exception),
            Context = request.Context,
            Data = data,
            HasData = data is not null,
        };
    }

    private static CultureInfo? ToCultureInfo(string? language)
    {
        return string.IsNullOrWhiteSpace(language)
            ? null
            : CultureInfo.GetCultureInfo(language);
    }
}
