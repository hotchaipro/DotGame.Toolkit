# Toolkit content pipeline tests

Regression tests for the toolkit's content pipeline: its build tool (`pipeline/tool/`), its MSBuild
tasks (`pipeline/tasks/`) and its targets (`pipeline/`). They cover the packer, PBON, fonts, kerning,
atlases, sound banks and sprite fonts.

They link the framework's `test/DotGame.Framework.ContentPipeline.Tests/Support/` folder and use its
fake tool, so they work as the framework's tests do; its README says how. The project tells that
code where the toolkit is through assembly metadata: `DotGameLibraryDir` (this repository),
`DotGameLibraryContentTargets` (the toolkit's targets, which `ContentProject.ImportLibrary` imports
after the framework's) and `DotGameLibraryBuildToolProperty` and `DotGameLibraryBuildTool` (the
toolkit's build tool, beside the tests). `ToolkitPaths` has the toolkit's own paths.

```bash
dotnet test --project test/DotGame.Toolkit.ContentPipeline.Tests
```

The integration tests run the toolkit's real build tool, and so its real packer, with fake encoders.
`RealToolkitToolTests` use the encoders on `PATH` or in the Vulkan SDK, and skip with a reason when
one isn't installed. The goldens are in `Goldens/goldens.txt` here.
