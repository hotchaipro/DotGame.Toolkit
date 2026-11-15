// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

using Microsoft.VisualStudio.ProjectSystem;

namespace DotGame.Toolkit.VisualStudio;

/// <summary>The extension's images, as Images\DotGame.imagemanifest declares them.</summary>
internal static class ContentFolderImages
{
    public static readonly Guid Catalog = new("8b2f7c1e-5a3d-4e6f-9c0b-1d2e3f4a5b6c");

    public static readonly ProjectImageMoniker ContentFolder = new(Catalog, 1);
    public static readonly ProjectImageMoniker ContentFolderOpen = new(Catalog, 2);
    public static readonly ProjectImageMoniker AtlasFolder = new(Catalog, 3);
    public static readonly ProjectImageMoniker AtlasFolderOpen = new(Catalog, 4);
    public static readonly ProjectImageMoniker BankFolder = new(Catalog, 5);
    public static readonly ProjectImageMoniker BankFolderOpen = new(Catalog, 6);
}
