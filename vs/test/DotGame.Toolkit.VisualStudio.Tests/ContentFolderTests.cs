// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

using Xunit;

namespace DotGame.Toolkit.VisualStudio.Tests;

public sealed class ContentFolderTests : IDisposable
{
    // The toolkit's defaults and LinkBase, as its targets have them, in an import (not the project file)
    private const string ToolkitImport = """
        <Project>
            <ItemDefinitionGroup>
                <SoundBankContent><Name>soundbank</Name></SoundBankContent>
                <TextureAtlasContent><Name>spritesheet</Name></TextureAtlasContent>
            </ItemDefinitionGroup>
            <ItemGroup>
                <TextureAtlasContent Update="@(TextureAtlasContent)">
                    <Link Condition="'%(Link)' == '' and '%(LinkBase)' != ''">%(LinkBase)\%(RecursiveDir)%(Filename)%(Extension)</Link>
                </TextureAtlasContent>
                <SoundBankContent Update="@(SoundBankContent)">
                    <Link Condition="'%(Link)' == '' and '%(LinkBase)' != ''">%(LinkBase)\%(RecursiveDir)%(Filename)%(Extension)</Link>
                </SoundBankContent>
            </ItemGroup>
        </Project>
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), "DotGame.Toolkit.VisualStudio.Tests", Guid.NewGuid().ToString("N"));
    private readonly ProjectCollection collection = new();

    public ContentFolderTests()
    {
        foreach (string file in new[] { @"Content\Sprites\a.png", @"Content\Sprites\b.png", @"Content\Other\c.png", @"Content\Sounds\x.wav", @"Content\Sounds\y.wav" })
        {
            string path = Path.Combine(this.root, file.Replace('\\', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }

        Directory.CreateDirectory(Path.Combine(this.root, "game"));
        File.WriteAllText(Path.Combine(this.root, "toolkit.targets"), ToolkitImport);
    }

    public void Dispose()
    {
        this.collection.Dispose();
        Directory.Delete(this.root, recursive: true);
    }

    [Fact]
    public void FindsTheLinkedItemsOfAFolder()
    {
        Project project = this.Load("""
            <TextureAtlasContent Include="..\Content\Sprites\*.png">
                <LinkBase>Content\Sprites</LinkBase>
            </TextureAtlasContent>
            """);

        ContentFolder? folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites\");

        Assert.NotNull(folder);
        Assert.Equal(2, folder.Items.Count);
        Assert.Equal("spritesheet", folder.GetValue("Name"));
        Assert.Null(ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sounds"));
        Assert.Null(ContentFolder.Find(project, ContentGroupKind.SoundBank, @"Content\Sprites"));
        Assert.Null(ContentFolder.Find(project, ContentGroupKind.TextureAtlas, "Content"));
    }

    [Fact]
    public void FindsTheItemsOfARealFolder()
    {
        Project project = this.LoadAtRoot("""
            <TextureAtlasContent Include="Content\Sprites\*.png" />
            """);

        ContentFolder? folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, "Content/Sprites");

        Assert.NotNull(folder);
        Assert.Equal(2, folder.Items.Count);
    }

    [Fact]
    public void WritesASettingOnTheWildcardElement()
    {
        Project project = this.Load("""
            <TextureAtlasContent Include="..\Content\Sprites\*.png">
                <LinkBase>Content\Sprites</LinkBase>
            </TextureAtlasContent>
            """);
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!;

        Assert.Null(folder.CheckWritable("Compression"));
        folder.SetValue("Compression", "Lossy");
        folder.SetValue("LossyQuality", "7");

        ProjectItemElement element = Assert.Single(project.Xml.Items);
        Assert.Equal(@"..\Content\Sprites\*.png", element.Include);
        Assert.Equal(["LinkBase", "Compression", "LossyQuality"], element.Metadata.Select(metadata => metadata.Name));
        Assert.All(element.Metadata, metadata => Assert.False(metadata.ExpressedAsAttribute));
        project.ReevaluateIfNecessary();
        Assert.Equal("7", ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!.GetValue("LossyQuality"));
    }

    [Fact]
    public void ChangesAnExistingSettingWhereItIs()
    {
        Project project = this.Load("""
            <SoundBankContent Include="..\Content\Sounds\*.wav" LosslessFormat="WAV" Preload="true">
                <LinkBase>Content\Sounds</LinkBase>
            </SoundBankContent>
            """);
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.SoundBank, @"Content\Sounds")!;

        folder.SetValue("LosslessFormat", "FLAC");

        ProjectMetadataElement metadata = Assert.Single(project.Xml.Items.Single().Metadata, metadata => metadata.Name == "LosslessFormat");
        Assert.Equal("FLAC", metadata.Value);
        Assert.True(metadata.ExpressedAsAttribute);
    }

    [Fact]
    public void TheDefaultOrNothingRemovesTheSetting()
    {
        Project project = this.Load("""
            <SoundBankContent Include="..\Content\Sounds\*.wav">
                <LinkBase>Content\Sounds</LinkBase>
                <LosslessFormat>FLAC</LosslessFormat>
                <Preload>true</Preload>
            </SoundBankContent>
            """);
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.SoundBank, @"Content\Sounds")!;

        folder.SetValue("LosslessFormat", "default");
        folder.SetValue("Preload", null);

        Assert.Equal(["LinkBase"], project.Xml.Items.Single().Metadata.Select(metadata => metadata.Name));
    }

    [Fact]
    public void ClearsTheSettingFromUpdateElementsOfTheFolder()
    {
        Project project = this.Load("""
            <TextureAtlasContent Include="..\Content\Sprites\*.png">
                <LinkBase>Content\Sprites</LinkBase>
            </TextureAtlasContent>
            <TextureAtlasContent Update="..\Content\Sprites\a.png" Compression="Lossy" Preload="true" />
            """);
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!;
        Assert.Null(folder.GetValue("Compression"));

        Assert.Null(folder.CheckWritable("Compression"));
        folder.SetValue("Compression", "Lossy");

        ProjectItemElement update = project.Xml.Items.Single(element => element.Update.Length > 0);
        Assert.Equal(["Preload"], update.Metadata.Select(metadata => metadata.Name));
        project.ReevaluateIfNecessary();
        Assert.Equal("Lossy", ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!.GetValue("Compression"));
    }

    [Fact]
    public void SwitchingCompressionDropsTheOtherQuality()
    {
        Project project = this.Load("""
            <TextureAtlasContent Include="..\Content\Sprites\*.png">
                <LinkBase>Content\Sprites</LinkBase>
                <LosslessQuality>9</LosslessQuality>
            </TextureAtlasContent>
            """);
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!;

        folder.SetValue("Compression", "Lossy");

        Assert.Equal(["LinkBase", "Compression"], project.Xml.Items.Single().Metadata.Select(metadata => metadata.Name));
    }

    [Fact]
    public void RefusesAnElementThatAlsoMakesOtherFoldersItems()
    {
        Project project = this.Load("""
            <TextureAtlasContent Include="..\Content\**\*.png">
                <LinkBase>Content</LinkBase>
            </TextureAtlasContent>
            """);
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!;

        string? error = folder.CheckWritable("Compression");

        Assert.NotNull(error);
        Assert.Contains("outside Content\\Sprites", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAnUpdateThatAlsoSetsOtherFoldersItems()
    {
        Project project = this.Load("""
            <TextureAtlasContent Include="..\Content\Sprites\*.png">
                <LinkBase>Content\Sprites</LinkBase>
            </TextureAtlasContent>
            <TextureAtlasContent Include="..\Content\Other\*.png">
                <LinkBase>Content\Other</LinkBase>
            </TextureAtlasContent>
            <TextureAtlasContent Update="..\Content\**\*.png" Preload="true" />
            """);
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!;

        Assert.Null(folder.CheckWritable("Compression"));
        Assert.NotNull(folder.CheckWritable("Preload"));
    }

    [Fact]
    public void SeveralAtlasesInAFolderShareTheirOtherSettings()
    {
        Project project = this.Load("""
            <TextureAtlasContent Include="..\Content\Sprites\*.png">
                <LinkBase>Content\Sprites</LinkBase>
            </TextureAtlasContent>
            <TextureAtlasContent Update="..\Content\Sprites\b.png" Name="icons" />
            """);
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!;

        Assert.True(folder.HasSeveralGroups);
        Assert.Null(folder.GetValue("Name"));
        Assert.NotNull(folder.CheckWritable("Name"));
        Assert.Null(folder.CheckWritable("Preload"));
    }

    [Fact]
    public void RenamesTheAtlas()
    {
        Project project = this.Load("""
            <TextureAtlasContent Include="..\Content\Sprites\*.png">
                <LinkBase>Content\Sprites</LinkBase>
            </TextureAtlasContent>
            """);
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!;

        Assert.Null(folder.CheckWritable("Name"));
        folder.SetValue("Name", "sprites");
        project.ReevaluateIfNecessary();

        Assert.Equal("sprites", ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!.GetValue("Name"));
    }

    [Fact]
    public void RefusesItemsDeclaredInAnImport()
    {
        File.WriteAllText(Path.Combine(this.root, "items.props"), """
            <Project>
                <ItemGroup>
                    <TextureAtlasContent Include="$(MSBuildThisFileDirectory)Content\Sprites\*.png" LinkBase="Content\Sprites" />
                </ItemGroup>
            </Project>
            """);
        Project project = this.Load(string.Empty, """<Import Project="..\items.props" />""");
        ContentFolder folder = ContentFolder.Find(project, ContentGroupKind.TextureAtlas, @"Content\Sprites")!;

        Assert.Contains("items.props", folder.CheckWritable("Compression"), StringComparison.Ordinal);
    }

    private Project Load(string items, string imports = "")
    {
        return this.LoadFrom(Path.Combine(this.root, "game", "Game.csproj"), items, imports, @"..\toolkit.targets");
    }

    private Project LoadAtRoot(string items)
    {
        return this.LoadFrom(Path.Combine(this.root, "Game.csproj"), items, string.Empty, "toolkit.targets");
    }

    private Project LoadFrom(string path, string items, string imports, string toolkit)
    {
        File.WriteAllText(path, $"""
            <Project>
                {imports}
                <ItemGroup>
                    {items}
                </ItemGroup>
                <Import Project="{toolkit}" />
            </Project>
            """);
        return new Project(path, null, null, this.collection);
    }
}
