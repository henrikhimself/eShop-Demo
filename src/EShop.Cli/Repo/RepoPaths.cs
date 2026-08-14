// <copyright file="RepoPaths.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Repo;

// Centralizes the well-known repo-relative paths every command needs, resolved
// once against the located repo root.
internal sealed class RepoPaths(string root)
{
    // Shared Docker tag for the utility image. It is not repo-relative, so it lives
    // here as a constant rather than a Path.Combine-derived property.
    public const string UtilityImageTag = "eshop-utility:local";

    public string Root { get; } = root;

    public string Solution => Path.Combine(Root, "EShop.slnx");

    public string SellerPortalWebDir => Path.Combine(Root, "src", "EShop.SellerPortal.Web");

    public string SellerPortalBffProject =>
        Path.Combine(Root, "src", "EShop.SellerPortal.Bff", "EShop.SellerPortal.Bff.csproj");

    public string SellerPortalBffOpenApiJson =>
        Path.Combine(Root, "src", "EShop.SellerPortal.Bff", "obj", "openapi", "EShop.SellerPortal.Bff.json");

    public string GeneratedApiSchema => Path.Combine(SellerPortalWebDir, "lib", "api-schema.d.ts");

    public string AppHostProject => Path.Combine(Root, "src", "EShop.AppHost", "EShop.AppHost.csproj");

    public string E2ETestProject =>
        Path.Combine(Root, "test", "EShop.AppHost.E2ETests", "EShop.AppHost.E2ETests.csproj");

    public string ContainerfilePath => Path.Combine(Root, "scripts", "Containerfile");

    public string GlobalJsonPath => Path.Combine(Root, "global.json");

    public string NvmrcPath => Path.Combine(Root, ".nvmrc");

    public string PackageJsonPath => Path.Combine(Root, "package.json");

    public string DirectoryPackagesPropsPath => Path.Combine(Root, "Directory.Packages.props");

    public string ContainerEnvPath => Path.Combine(Root, "scripts", "container.env");

    public string TmpDir => Path.Combine(Root, "tmp");

    public string TestResultsDir => Path.Combine(TmpDir, "TestResults");
}
