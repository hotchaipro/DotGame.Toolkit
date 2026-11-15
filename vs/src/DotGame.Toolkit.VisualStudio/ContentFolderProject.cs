// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.ProjectSystem;

namespace DotGame.Toolkit.VisualStudio;

/// <summary>A folder's atlas or bank settings as they were read: each one's value, or null if its items differ.</summary>
internal sealed class ContentFolderSnapshot
{
    public ContentFolderSnapshot(IReadOnlyDictionary<string, string?> values, bool hasSeveralGroups)
    {
        this.Values = values;
        this.HasSeveralGroups = hasSeveralGroups;
    }

    public IReadOnlyDictionary<string, string?> Values { get; }

    public bool HasSeveralGroups { get; }
}

/// <summary>Reads and writes a folder's settings in a project open in Visual Studio, under the project system's locks.</summary>
internal sealed class ContentFolderProject
{
    public ContentFolderProject(ConfiguredProject project, ContentGroupKind kind, string folder)
    {
        this.Project = project;
        this.Kind = kind;
        this.Folder = folder;
    }

    public ConfiguredProject Project { get; }

    public ContentGroupKind Kind { get; }

    /// <summary>The folder, relative to the project.</summary>
    public string Folder { get; }

    /// <summary>A folder path as Visual Studio gives it (full, or relative to the project), relative to the project.</summary>
    public static string RelativeFolder(UnconfiguredProject project, string path)
    {
        if (Path.IsPathRooted(path))
        {
            string directory = Path.GetDirectoryName(project.FullPath)!.TrimEnd('\\') + "\\";
            string full = Path.GetFullPath(path);
            if (full.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            {
                path = full.Substring(directory.Length);
            }
        }

        return ContentFolder.NormalizeFolder(path);
    }

    /// <summary>The folder's settings, or null if it has no items of the kind.</summary>
    public async Task<ContentFolderSnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        IProjectLockService locks = this.Project.UnconfiguredProject.ProjectService.Services.ProjectLockService!;
        return await locks.ReadLockAsync(
            async access =>
            {
                Project project = await access.GetProjectAsync(this.Project, cancellationToken);
                ContentFolder? folder = ContentFolder.Find(project, this.Kind, this.Folder);
                if (folder == null)
                {
                    return null;
                }

                Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);
                foreach (string name in this.Kind.Defaults.Keys)
                {
                    values[name] = folder.GetValue(name);
                }

                return new ContentFolderSnapshot(values, folder.HasSeveralGroups);
            },
            cancellationToken);
    }

    /// <summary>Sets a setting on the folder's items in the project file; throws with the reason if it can't.</summary>
    public async Task WriteAsync(string name, string? value, CancellationToken cancellationToken)
    {
        IProjectLockService locks = this.Project.UnconfiguredProject.ProjectService.Services.ProjectLockService!;
        await locks.WriteLockAsync(
            async access =>
            {
                await access.CheckoutAsync(this.Project.UnconfiguredProject.FullPath);
                Project project = await access.GetProjectAsync(this.Project, cancellationToken);
                ContentFolder folder = ContentFolder.Find(project, this.Kind, this.Folder)
                    ?? throw new InvalidOperationException($"{this.Folder} has no {this.Kind.ItemType} items any more.");
                string? error = folder.CheckWritable(name);
                if (error != null)
                {
                    throw new InvalidOperationException(error);
                }

                folder.SetValue(name, value);
            },
            cancellationToken);

        await this.Project.UnconfiguredProject.SaveAsync();
    }
}
