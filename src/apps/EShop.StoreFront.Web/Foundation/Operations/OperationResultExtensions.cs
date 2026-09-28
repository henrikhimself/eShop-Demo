// <copyright file="OperationResultExtensions.cs" company="Henrik Jensen">
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

namespace Hj.EShop.StoreFront.Web.Foundation.Operations;

internal static class OperationResultExtensions
{
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
            Error = ToError(exception),
            HasError = true,
            Context = request.Context,
        };
    }

    [DebuggerStepThrough]
    public static OperationDataResponse<T> Fail<T>(this OperationRequest request, Exception exception, T? data = default)
    {
        return new OperationDataResponse<T>()
        {
            Error = ToError(exception),
            HasError = true,
            Context = request.Context,
            Data = data,
            HasData = data is not null,
        };
    }

    private static InvalidOperationException ToError(Exception exception)
    {
        return exception is InvalidOperationException invalidOperation
            ? invalidOperation
            : new InvalidOperationException("Operation failed", exception);
    }
}
