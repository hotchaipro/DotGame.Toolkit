// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks.Dataflow;

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace DotGame.Toolkit.VisualStudio;

/// <summary>
/// Gives DotGame folders their icons in Solution Explorer: the content folder (Content, or wwwroot\Content in a web
/// project), and each folder with texture atlas or sound bank items directly in it. Which folders those are depends on
/// the project's items, so the icons come from a data source that makes a new <see cref="ContentFolderIconProvider"/>
/// whenever they change, and the project tree updates with it.
/// </summary>
[Export(typeof(IProjectTreePropertiesProviderDataSource))]
[AppliesTo("DotGame")]
internal sealed class ContentFolderIconsDataSource : ChainedProjectValueDataSourceBase<IProjectTreePropertiesProvider>, IProjectTreePropertiesProviderDataSource
{
    private readonly UnconfiguredProject project;
    private readonly IActiveConfiguredProjectSubscriptionService subscriptions;

    [ImportingConstructor]
    public ContentFolderIconsDataSource(UnconfiguredProject project, IActiveConfiguredProjectSubscriptionService subscriptions)
        : base(project, synchronousDisposal: false, registerDataSource: false)
    {
        this.project = project;
        this.subscriptions = subscriptions;
    }

    protected override IDisposable? LinkExternalInput(ITargetBlock<IProjectVersionedValue<IProjectTreePropertiesProvider>> targetBlock)
    {
        IProjectValueDataSource<IProjectSubscriptionUpdate> source = this.subscriptions.SourceItemsRuleSource;
        this.JoinUpstreamDataSources(source);

        string directory = Path.GetDirectoryName(this.project.FullPath)!;
        TransformBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>, IProjectVersionedValue<IProjectTreePropertiesProvider>> transform = new(
            update => new ProjectVersionedValue<IProjectTreePropertiesProvider>(
                ContentFolderIconProvider.From(directory, update.Value.CurrentState),
                update.DataSourceVersions));

        DataflowLinkOptions options = new() { PropagateCompletion = true };
        IDisposable fromSource = source.SourceBlock.LinkTo(transform, options);
        IDisposable toTarget = transform.LinkTo(targetBlock, options);
        return new Links(fromSource, toTarget);
    }

    private sealed class Links : IDisposable
    {
        private readonly IDisposable[] links;

        public Links(params IDisposable[] links)
        {
            this.links = links;
        }

        public void Dispose()
        {
            foreach (IDisposable link in this.links)
            {
                link.Dispose();
            }
        }
    }
}

/// <summary>
/// The icons for one state of a project's items: immutable, as the project tree requires of a provider from a data
/// source. Folders are matched by their full path, which the tree gives every folder as its FullPath metadata.
/// </summary>
internal sealed class ContentFolderIconProvider : IProjectTreePropertiesProvider
{
    // The web SDK's flag for wwwroot
    private static readonly ProjectTreeFlags WebRootFolder = ProjectTreeFlags.Create("WebRootFolder");

    private readonly ImmutableHashSet<string> atlasFolders;
    private readonly ImmutableHashSet<string> bankFolders;

    private ContentFolderIconProvider(ImmutableHashSet<string> atlasFolders, ImmutableHashSet<string> bankFolders)
    {
        this.atlasFolders = atlasFolders;
        this.bankFolders = bankFolders;
    }

    public static ContentFolderIconProvider From(string projectDirectory, IImmutableDictionary<string, IProjectRuleSnapshot> items)
    {
        return new ContentFolderIconProvider(
            Folders(projectDirectory, items, ContentGroupKind.TextureAtlas.ItemType),
            Folders(projectDirectory, items, ContentGroupKind.SoundBank.ItemType));
    }

    public void CalculatePropertyValues(IProjectTreeCustomizablePropertyContext propertyContext, IProjectTreeCustomizablePropertyValues propertyValues)
    {
        if (!propertyContext.IsFolder || propertyValues.Flags.Contains(ProjectTreeFlags.Common.ProjectRoot))
        {
            return;
        }

        ProjectImageMoniker? closed = null;
        ProjectImageMoniker? open = null;
        string? path = propertyContext.Metadata != null && propertyContext.Metadata.TryGetValue("FullPath", out string? fullPath) ? Normalize(fullPath) : null;
        if (string.Equals(propertyContext.ItemName, "Content", StringComparison.OrdinalIgnoreCase)
            && (propertyContext.ParentNodeFlags.Contains(ProjectTreeFlags.Common.ProjectRoot)
                || propertyContext.ParentNodeFlags.Contains(WebRootFolder)))
        {
            // The content folder's glyph, whatever it holds
            (closed, open) = (ContentFolderImages.ContentFolder, ContentFolderImages.ContentFolderOpen);
        }
        else if (path != null && this.atlasFolders.Contains(path))
        {
            (closed, open) = (ContentFolderImages.AtlasFolder, ContentFolderImages.AtlasFolderOpen);
        }
        else if (path != null && this.bankFolders.Contains(path))
        {
            (closed, open) = (ContentFolderImages.BankFolder, ContentFolderImages.BankFolderOpen);
        }

        if (closed != null)
        {
            propertyValues.Icon = closed;
            propertyValues.ExpandedIcon = open;
        }
    }

    /// <summary>The full paths of the folders Solution Explorer shows an item type's items in.</summary>
    private static ImmutableHashSet<string> Folders(string projectDirectory, IImmutableDictionary<string, IProjectRuleSnapshot> items, string itemType)
    {
        ImmutableHashSet<string>.Builder folders = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        if (!items.TryGetValue(itemType, out IProjectRuleSnapshot? snapshot))
        {
            return folders.ToImmutable();
        }

        foreach (KeyValuePair<string, IImmutableDictionary<string, string>> item in snapshot.Items)
        {
            string path = item.Value.TryGetValue("Link", out string? link) && link.Length > 0 ? link : item.Key;
            int separator = path.LastIndexOfAny(['\\', '/']);
            if (separator < 0)
            {
                continue;
            }

            try
            {
                folders.Add(Normalize(Path.Combine(projectDirectory, path.Substring(0, separator))));
            }
            catch (ArgumentException)
            {
                // A path the tree couldn't show either
            }
        }

        return folders.ToImmutable();
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path.Replace('/', '\\')).TrimEnd('\\');
    }
}
