// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.ContentPipeline.Testing;

/// <summary>
/// Where the toolkit's things are: its repository, which the framework's <see cref="Paths"/> has as
/// the library's, and this test project's sources, where its goldens are.
/// </summary>
internal static class ToolkitPaths
{
    public static string Root => Paths.LibraryRootOrNull
        ?? throw new InvalidOperationException("The test project doesn't say where the toolkit is (DotGameLibraryDir).");

    public static string Tests => Path.Combine(Root, "test", "DotGame.Toolkit.ContentPipeline.Tests");
}
