// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Threading.Tasks;
using DotGame.Framework.Desktop;

namespace Sample;

internal static class Program
{
    public static async Task Main()
    {
        var engine = new DesktopGameEngine(new()
        {
            Title = "DotGame Toolkit Sample",
            WindowSize = WindowSize.Fit(4f / 3f, maxScreenPercent: 85),
        });
        await engine.RunAsync(new ToolkitSample());
    }
}
