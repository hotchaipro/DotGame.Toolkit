// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using Android.App;
using Android.OS;
using Android.Widget;
using DotGame.Framework.Android;

namespace Sample;

[Activity(
    Label = "DotGame Toolkit Sample",
    MainLauncher = true,
    ConfigurationChanges = AndroidGameEngine.ConfigurationChanges,
    Theme = "@style/DotGame.LaunchScreen")]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var hostView = new FrameLayout(this);
        this.SetContentView(hostView);

        new AndroidGameEngine(this, hostView).Run(new ToolkitSample());
    }
}
