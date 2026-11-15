// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace DotGame.Toolkit.VisualStudio;

/// <summary>
/// The items of one kind (atlas or bank) directly in one Solution Explorer folder, and the project file's
/// elements that make them. The pipeline makes one atlas or bank of a folder's items per Name, the folder being
/// where the item's Link puts it, or its own folder without one: the same folder Solution Explorer shows it in.
/// </summary>
internal sealed class ContentFolder
{
    private readonly Project project;

    private ContentFolder(Project project, ContentGroupKind kind, string folder, IReadOnlyList<ProjectItem> items)
    {
        this.project = project;
        this.Kind = kind;
        this.Folder = folder;
        this.Items = items;
    }

    public ContentGroupKind Kind { get; }

    /// <summary>The folder, relative to the project, e.g. Content\Sprites.</summary>
    public string Folder { get; }

    public IReadOnlyList<ProjectItem> Items { get; }

    /// <summary>Whether the folder's items make more than one atlas or bank (they have different Names).</summary>
    public bool HasSeveralGroups => this.Items.Select(item => item.GetMetadataValue("Name")).Distinct(StringComparer.OrdinalIgnoreCase).Skip(1).Any();

    /// <summary>The items of a kind directly in a folder, or null if it has none.</summary>
    public static ContentFolder? Find(Project project, ContentGroupKind kind, string folder)
    {
        string normalized = NormalizeFolder(folder);
        List<ProjectItem> items = [];
        foreach (ProjectItem item in project.GetItems(kind.ItemType))
        {
            if (string.Equals(FolderOf(item), normalized, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(item);
            }
        }

        return items.Count == 0 ? null : new ContentFolder(project, kind, normalized, items);
    }

    /// <summary>The folder Solution Explorer shows an item in, relative to the project, without a trailing separator.</summary>
    public static string FolderOf(ProjectItem item)
    {
        string path = item.GetMetadataValue("Link");
        if (path.Length == 0)
        {
            path = item.EvaluatedInclude;
        }

        int separator = path.LastIndexOfAny(['\\', '/']);
        return separator < 0 ? string.Empty : NormalizeFolder(path.Substring(0, separator));
    }

    /// <summary>A folder path, relative to the project, with backslashes and no trailing separator.</summary>
    public static string NormalizeFolder(string folder)
    {
        return folder.Replace('/', '\\').Trim('\\');
    }

    /// <summary>The folder's value of a setting: its items' value, or null if they have different values.</summary>
    public string? GetValue(string name)
    {
        string first = this.Items[0].GetMetadataValue(name);
        foreach (ProjectItem item in this.Items)
        {
            if (!string.Equals(item.GetMetadataValue(name), first, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return first;
    }

    /// <summary>
    /// Why the folder's settings can't be written, or null if they can: an element that makes the folder's items
    /// is outside the project file, or also makes items outside the folder, whose atlases or banks a change would
    /// change too.
    /// </summary>
    public string? CheckWritable(string name)
    {
        if (string.Equals(name, "Name", StringComparison.OrdinalIgnoreCase) && this.HasSeveralGroups)
        {
            return $"The items in {this.Folder} make more than one {this.Description} (they have different Names): set each one's Name on its files.";
        }

        foreach (ProjectItemElement element in this.IncludeElements())
        {
            if (!this.IsInProjectFile(element))
            {
                return $"Some of the {this.Kind.ItemType} items in {this.Folder} are declared in {element.ContainingProject.FullPath}, not in the project file: change them there.";
            }

            foreach (ProjectItem item in this.project.GetItems(this.Kind.ItemType))
            {
                if (item.Xml == element && !string.Equals(FolderOf(item), this.Folder, StringComparison.OrdinalIgnoreCase))
                {
                    return $"<{this.Kind.ItemType} Include=\"{element.Include}\"> also makes items outside {this.Folder} (such as {item.EvaluatedInclude}), "
                        + $"whose settings would change too: give {this.Folder}'s items an element of their own, then set them here.";
                }
            }
        }

        foreach (ProjectItemElement element in this.UpdateElements(name))
        {
            foreach (ProjectItem item in this.project.GetItems(this.Kind.ItemType))
            {
                if (!string.Equals(FolderOf(item), this.Folder, StringComparison.OrdinalIgnoreCase)
                    && this.project.GetItemProvenance(item).Any(result => result.ItemElement == element))
                {
                    return $"<{this.Kind.ItemType} Update=\"{element.Update}\"> sets {name} on items outside {this.Folder} too (such as {item.EvaluatedInclude}): change it there.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Sets a setting on every element that makes the folder's items, as a child element of each Include element
    /// (a wildcard stays a wildcard). The setting is removed from the project file's Update elements that set it on
    /// the folder's items, so the folder's value is the one used. An empty
    /// value, or the pipeline's default, removes the setting instead. Call <see cref="CheckWritable"/> first.
    /// </summary>
    public void SetValue(string name, string? value)
    {
        bool remove = string.IsNullOrEmpty(value) || this.IsDefault(name, value!);
        foreach (ProjectItemElement element in this.IncludeElements())
        {
            if (remove)
            {
                RemoveMetadata(element, name);
            }
            else
            {
                SetMetadata(element, name, value!);
            }
        }

        foreach (ProjectItemElement element in this.UpdateElements(name))
        {
            RemoveMetadata(element, name);
        }

        // A lossless atlas can't have LossyQuality, nor a lossy one LosslessQuality: drop the other one's quality
        if (this.Kind == ContentGroupKind.TextureAtlas && string.Equals(name, "Compression", StringComparison.OrdinalIgnoreCase))
        {
            string other = string.Equals(value, "Lossy", StringComparison.OrdinalIgnoreCase) ? "LosslessQuality" : "LossyQuality";
            foreach (ProjectItemElement element in this.IncludeElements().Concat(this.UpdateElements(other)))
            {
                RemoveMetadata(element, other);
            }
        }
    }

    private string Description => this.Kind == ContentGroupKind.TextureAtlas ? "texture atlas" : "sound bank";

    private bool IsDefault(string name, string value)
    {
        if (!this.Kind.Defaults.TryGetValue(name, out string? defaultValue))
        {
            return false;
        }

        // An atlas with GPU compression is lossy unless it says otherwise (an error, but one to keep visible)
        if (this.Kind == ContentGroupKind.TextureAtlas
            && string.Equals(name, "Compression", StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.GetValue("GpuCompression"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(value, defaultValue, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsInProjectFile(ProjectElement element)
    {
        return string.Equals(element.ContainingProject.FullPath, this.project.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The Include elements that make the folder's items.</summary>
    private IEnumerable<ProjectItemElement> IncludeElements()
    {
        return this.Items.Select(item => item.Xml).Distinct();
    }

    /// <summary>The project file's Update elements that set a setting on any of the folder's items.</summary>
    private HashSet<ProjectItemElement> UpdateElements(string name)
    {
        HashSet<ProjectItemElement> elements = [];
        foreach (ProjectItem item in this.Items)
        {
            foreach (ProvenanceResult result in this.project.GetItemProvenance(item))
            {
                if (result.Operation == Operation.Update
                    && this.IsInProjectFile(result.ItemElement)
                    && result.ItemElement.Metadata.Any(metadata => string.Equals(metadata.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    elements.Add(result.ItemElement);
                }
            }
        }

        return elements;
    }

    private static void SetMetadata(ProjectItemElement element, string name, string value)
    {
        ProjectMetadataElement? existing = null;
        foreach (ProjectMetadataElement metadata in element.Metadata.ToList())
        {
            if (!string.Equals(metadata.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (existing == null && metadata.Condition.Length == 0)
            {
                existing = metadata;
            }
            else
            {
                // A conditional or second value would override the folder's
                element.RemoveChild(metadata);
            }
        }

        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            element.AddMetadata(name, value);
        }
    }

    private static void RemoveMetadata(ProjectItemElement element, string name)
    {
        foreach (ProjectMetadataElement metadata in element.Metadata.ToList())
        {
            if (string.Equals(metadata.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                element.RemoveChild(metadata);
            }
        }
    }
}
