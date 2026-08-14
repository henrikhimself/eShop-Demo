// <copyright file="DiagnosticReportTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Diagnostics;
using Xunit;

namespace Hj.EShop.Cli.Tests.Diagnostics;

public sealed class DiagnosticReportTests
{
    [Fact]
    public void Merge_DuplicateLineAcrossSources_KeepsFirstOccurrenceOnly()
    {
        IReadOnlyList<Diagnostic> first = [new Diagnostic("Foo.cs(1,1): error CS0001: x"), new Diagnostic("Bar.cs(2,2): error CS0002: y")];
        IReadOnlyList<Diagnostic> second = [new Diagnostic("Foo.cs(1,1): error CS0001: x"), new Diagnostic("Baz.cs(3,3): error CS0003: z")];

        IReadOnlyList<Diagnostic> merged = DiagnosticReport.Merge(first, second);

        Assert.Equal(
            ["Foo.cs(1,1): error CS0001: x", "Bar.cs(2,2): error CS0002: y", "Baz.cs(3,3): error CS0003: z"],
            merged.Select(d => d.Text));
    }

    [Fact]
    public void Merge_NoSources_ReturnsEmpty()
    {
        Assert.Empty(DiagnosticReport.Merge());
    }
}
