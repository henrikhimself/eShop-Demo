// <copyright file="OperationExtensionsTests.cs" company="Henrik Jensen">
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

using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using Hj.EShop.StoreFront.Web.Foundation.Operations;
using Hj.EShop.StoreFront.Web.Foundation.Operations.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Hj.EShop.StoreFront.Web.Tests.Foundation.Operations;

public sealed class OperationExtensionsTests
{
    [Fact]
    public void ClaimsPrincipal_WithClaimsPrincipal_ReturnsTheTypedPrincipal()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(authenticationType: "Cookies"));
        OperationContext context = new()
        {
            Principal = principal,
        };

        Assert.Same(principal, context.ClaimsPrincipal);
    }

    [Fact]
    public void ClaimsPrincipal_WithoutClaimsPrincipal_ReturnsNull()
    {
        OperationContext context = new()
        {
            Principal = new TestPrincipal(),
        };

        Assert.Null(context.ClaimsPrincipal);
    }

    [Fact]
    public void ClaimsIdentity_WithClaimsPrincipal_ReturnsTheTypedIdentity()
    {
        ClaimsIdentity identity = new(authenticationType: "Cookies");
        OperationContext context = new()
        {
            Principal = new ClaimsPrincipal(identity),
        };

        Assert.Same(identity, context.ClaimsIdentity);
    }

    [Fact]
    public void IsUserAuthenticated_WithoutAuthenticatedClaimsIdentity_ReturnsFalse()
    {
        OperationContext unauthenticatedContext = new()
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity()),
        };
        OperationContext missingPrincipalContext = new();

        Assert.False(unauthenticatedContext.IsUserAuthenticated);
        Assert.False(missingPrincipalContext.IsUserAuthenticated);
    }

    [Fact]
    public void IsUserAuthenticated_WithAuthenticatedClaimsIdentity_ReturnsTrue()
    {
        OperationContext context = new()
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Cookies")),
        };

        Assert.True(context.IsUserAuthenticated);
    }

    [Fact]
    public void CreateOperationRequest_FromHttpContext_CopiesHttpContextState()
    {
        CancellationToken cancellationToken = new(canceled: true);
        ClaimsPrincipal principal = new(new ClaimsIdentity(authenticationType: "Cookies"));
        DefaultHttpContext httpContext = new()
        {
            RequestAborted = cancellationToken,
            User = principal,
        };

        OperationRequest request = httpContext.CreateOperationRequest();

        Assert.Equal(cancellationToken, request.CancellationToken);
        Assert.Same(principal, request.Context.Principal);
        Assert.Null(request.Context.ContentLink);
        Assert.Null(request.Context.Language);
    }

    [Fact]
    public void CreateOperationRequestOfT_FromHttpContext_CopiesHttpContextStateAndData()
    {
        DefaultHttpContext httpContext = new();

        OperationDataRequest<string> request = httpContext.CreateOperationRequest("hello");

        Assert.Equal("hello", request.Data);
        Assert.Equal(httpContext.RequestAborted, request.CancellationToken);
        Assert.Same(httpContext.User, request.Context.Principal);
        Assert.Null(request.Context.ContentLink);
        Assert.Null(request.Context.Language);
    }

    [Fact]
    public void CreateOperationRequest_FromController_UsesControllerHttpContext()
    {
        DefaultHttpContext httpContext = new();
        TestController controller = new()
        {
            ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext,
            },
        };

        OperationRequest request = controller.CreateOperationRequest();

        Assert.Equal(httpContext.RequestAborted, request.CancellationToken);
        Assert.Same(httpContext.User, request.Context.Principal);
        Assert.Null(request.Context.ContentLink);
        Assert.Null(request.Context.Language);
    }

    [Fact]
    public void CreateOperationRequestOfT_FromController_UsesControllerHttpContextAndData()
    {
        DefaultHttpContext httpContext = new();
        TestController controller = new()
        {
            ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext,
            },
        };

        OperationDataRequest<int> request = controller.CreateOperationRequest(42);

        Assert.Equal(42, request.Data);
        Assert.Equal(httpContext.RequestAborted, request.CancellationToken);
        Assert.Same(httpContext.User, request.Context.Principal);
        Assert.Null(request.Context.ContentLink);
        Assert.Null(request.Context.Language);
    }

    [Fact]
    public async Task OkTask_FromOperationRequest_ReturnsSuccessfulResponse()
    {
        OperationRequest request = CreateOperationRequest();

        OperationResponse response = await request.OkTask();

        Assert.True(response.IsSuccess);
        Assert.Null(response.Error);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public async Task OkTaskOfT_WithoutExplicitData_ReturnsSuccessfulResponseWithDefaultData()
    {
        OperationRequest request = CreateOperationRequest();

        OperationDataResponse<string> response = await request.OkTask<string>();

        Assert.True(response.IsSuccess);
        Assert.Null(response.Error);
        Assert.Null(response.Data);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public void Ok_FromOperationRequest_ReturnsSuccessfulResponse()
    {
        OperationRequest request = CreateOperationRequest();

        OperationResponse response = request.Ok();

        Assert.True(response.IsSuccess);
        Assert.Null(response.Error);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public void OkOfT_FromOperationRequest_ReturnsSuccessfulResponseWithData()
    {
        OperationRequest request = CreateOperationRequest();

        OperationDataResponse<string> response = request.Ok("done");

        Assert.True(response.IsSuccess);
        Assert.Null(response.Error);
        Assert.Equal("done", response.Data);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public async Task FailTask_WithMessage_CreatesInvalidOperationException()
    {
        OperationRequest request = CreateOperationRequest();

        OperationResponse response = await request.FailTask("bad request");

        Assert.False(response.IsSuccess);
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(response.Error);
        Assert.Equal("bad request", error.Message);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public async Task FailTaskOfT_WithoutExplicitData_PreservesDefaultData()
    {
        OperationRequest request = CreateOperationRequest();

        OperationDataResponse<string> response = await request.FailTask<string>("bad request");

        Assert.False(response.IsSuccess);
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(response.Error);
        Assert.Equal("bad request", error.Message);
        Assert.Null(response.Data);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public void Fail_WithMessage_CreatesInvalidOperationException()
    {
        OperationRequest request = CreateOperationRequest();

        OperationResponse response = request.Fail("bad request");

        Assert.False(response.IsSuccess);
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(response.Error);
        Assert.Equal("bad request", error.Message);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public void FailOfT_WithMessage_PreservesData()
    {
        OperationRequest request = CreateOperationRequest();

        OperationDataResponse<int> response = request.Fail("bad request", 7);

        Assert.False(response.IsSuccess);
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(response.Error);
        Assert.Equal("bad request", error.Message);
        Assert.Equal(7, response.Data);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public async Task FailTask_WithInvalidOperationException_PreservesTheException()
    {
        OperationRequest request = CreateOperationRequest();
        InvalidOperationException exception = new("known failure");

        OperationResponse response = await request.FailTask(exception);

        Assert.False(response.IsSuccess);
        Assert.Same(exception, response.Error);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public async Task FailTaskOfT_WithNonInvalidOperationException_WrapsTheExceptionAndPreservesData()
    {
        OperationRequest request = CreateOperationRequest();
        var exception = new NotSupportedException("boom");

        OperationDataResponse<string> response = await request.FailTask(exception, "partial data");

        Assert.False(response.IsSuccess);
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(response.Error);
        Assert.Equal("Operation failed", error.Message);
        Assert.Same(exception, error.InnerException);
        Assert.Equal("partial data", response.Data);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public void Fail_WithInvalidOperationException_PreservesTheException()
    {
        OperationRequest request = CreateOperationRequest();
        InvalidOperationException exception = new("known failure");

        OperationResponse response = request.Fail(exception);

        Assert.False(response.IsSuccess);
        Assert.Same(exception, response.Error);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public void Fail_WithNonInvalidOperationException_WrapsTheException()
    {
        OperationRequest request = CreateOperationRequest();
        var exception = new NotSupportedException("boom");

        OperationResponse response = request.Fail(exception);

        Assert.False(response.IsSuccess);
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(response.Error);
        Assert.Equal("Operation failed", error.Message);
        Assert.Same(exception, error.InnerException);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public void FailOfT_WithNonInvalidOperationException_WrapsTheExceptionAndPreservesData()
    {
        OperationRequest request = CreateOperationRequest();
        var exception = new NotSupportedException("boom");

        OperationDataResponse<string> response = request.Fail(exception, "partial data");

        Assert.False(response.IsSuccess);
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(response.Error);
        Assert.Equal("Operation failed", error.Message);
        Assert.Same(exception, error.InnerException);
        Assert.Equal("partial data", response.Data);
        Assert.Same(request.Context, response.Context);
    }

    [Fact]
    public void ToCultureInfo_WithNullOrWhitespace_ReturnsNull()
    {
        Assert.Null(InvokeToCultureInfo(null));
        Assert.Null(InvokeToCultureInfo(string.Empty));
        Assert.Null(InvokeToCultureInfo("   "));
    }

    [Fact]
    public void ToCultureInfo_WithLanguage_ReturnsCultureInfo()
    {
        CultureInfo? culture = InvokeToCultureInfo("sv-SE");

        Assert.NotNull(culture);
        Assert.Equal("sv-SE", culture.Name);
    }

    private static OperationRequest CreateOperationRequest()
    {
        return new OperationRequest()
        {
            CancellationToken = CancellationToken.None,
            Context = new OperationContext()
            {
                Principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Cookies")),
            },
        };
    }

    private static CultureInfo? InvokeToCultureInfo(string? language)
    {
        MethodInfo toCultureInfo = typeof(OperationExtensions).GetMethod(
            "ToCultureInfo",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("OperationExtensions.ToCultureInfo was not found.");

        return (CultureInfo?)toCultureInfo.Invoke(obj: null, [language]);
    }

    private sealed class TestController : Controller
    {
    }

    private sealed class TestPrincipal : IPrincipal
    {
        public IIdentity Identity { get; } = new GenericIdentity("alice");

        public bool IsInRole(string role)
        {
            return false;
        }
    }
}
