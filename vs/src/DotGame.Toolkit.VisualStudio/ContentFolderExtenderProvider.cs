// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

using EnvDTE;

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;

namespace DotGame.Toolkit.VisualStudio;

/// <summary>
/// Extends a folder's Properties window with its atlas's or sound bank's settings, for a folder of a DotGame.Toolkit
/// project (the DotGameToolkit capability) that has TextureAtlasContent or SoundBankContent items directly in it.
/// Other folders get nothing.
/// </summary>
[ComVisible(true)]
public sealed class ContentFolderExtenderProvider : IExtenderProvider
{
    private const string Capability = "DotGameToolkit";

    private readonly ContentGroupKind kind;
    private readonly JoinableTaskFactory joinableTaskFactory;

    internal ContentFolderExtenderProvider(ContentGroupKind kind, JoinableTaskFactory joinableTaskFactory)
    {
        this.kind = kind;
        this.joinableTaskFactory = joinableTaskFactory;
    }

    public bool CanExtend(string ExtenderCATID, string ExtenderName, object ExtendeeObject)
    {
        return this.Read(ExtendeeObject, out _, out _);
    }

    public object? GetExtender(string ExtenderCATID, string ExtenderName, object ExtendeeObject, IExtenderSite ExtenderSite, int Cookie)
    {
        if (!this.Read(ExtendeeObject, out ContentFolderProject? folder, out ContentFolderSnapshot? snapshot))
        {
            return null;
        }

        return this.kind == ContentGroupKind.TextureAtlas
            ? new TextureAtlasFolderExtender(folder!, snapshot!, this.joinableTaskFactory)
            : new SoundBankFolderExtender(folder!, snapshot!, this.joinableTaskFactory);
    }

    private bool Read(object extendee, out ContentFolderProject? folder, out ContentFolderSnapshot? snapshot)
    {
        folder = null;
        snapshot = null;
        if (extendee is not IVsBrowseObjectContext context
            || context.UnconfiguredProject is not UnconfiguredProject project
            || !project.Capabilities.AppliesTo(Capability))
        {
            return false;
        }

        IProjectPropertiesContext? properties = context.ProjectPropertiesContext;
        string? path = properties?.ItemName ?? properties?.File;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        try
        {
            ContentFolderProject? found = null;
            snapshot = this.joinableTaskFactory.Run(async () =>
            {
                ConfiguredProject? configured = context.ConfiguredProject ?? await project.GetSuggestedConfiguredProjectAsync();
                if (configured == null)
                {
                    return null;
                }

                found = new ContentFolderProject(configured, this.kind, ContentFolderProject.RelativeFolder(project, path!));
                return await found.ReadAsync(default);
            });
            folder = found;
        }
        catch (Exception exception) when (exception is InvalidOperationException or OperationCanceledException)
        {
            // The project is unloading, or its evaluation failed: show nothing
            return false;
        }

        return snapshot != null;
    }
}
