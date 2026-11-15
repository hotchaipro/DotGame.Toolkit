// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Runtime.Versioning;
using System.Threading.Tasks;
using DotGame.Framework.WebGL;

namespace Sample;

[SupportedOSPlatform("browser")]
internal static partial class Program
{
    public static async Task Main()
    {
        var engine = new WebGLGameEngine();
        await engine.RunAsync(new ToolkitSample());
    }
}
