// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Threading;

using EnvDTE;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;

using VSLangProj;

using Task = System.Threading.Tasks.Task;

namespace DotGame.Toolkit.VisualStudio;

/// <summary>
/// Loads with a solution and adds the atlas and sound bank settings to the Properties window of C# project folders
/// (automation extenders, which show only on the folders that have atlas or bank items: see
/// <see cref="ContentFolderExtenderProvider"/>).
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
// The image manifest's pack URIs load this assembly by name: its folder must be a binding path
[ProvideBindingPath]
[Guid(PackageGuid)]
public sealed class DotGameToolkitPackage : AsyncPackage
{
    public const string PackageGuid = "6d0c5b8e-6f0a-4f53-9e62-2a7f3c9d1b44";

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (await this.GetServiceAsync(typeof(DTE)) is not DTE dte)
        {
            return;
        }

        // For as long as Visual Studio runs: the package isn't unloaded before it closes
        foreach (ContentGroupKind kind in ContentGroupKind.All)
        {
            dte.ObjectExtenders.RegisterExtenderProvider(
                PrjBrowseObjectCATID.prjCATIDCSharpFolderBrowseObject,
                kind.ExtenderName,
                new ContentFolderExtenderProvider(kind, this.JoinableTaskFactory));
        }
    }
}
