// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using DotGame.Framework.Audio;
using DotGame.Framework.Content;
using DotGame.Toolkit.Audio;
using Xunit;

namespace DotGame.Toolkit.Tests.Audio;

/// <summary>
/// The sound bank manifest's format, loading a bank from it, and sound segments.
/// </summary>
public sealed class SoundBankTests
{
    private static Task<SoundBankManifest> ReadManifest(
        string json)
    {
        return SoundBankManifest.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }

    private static FakeContent ContentWithBank(
        string path,
        string json)
    {
        var content = new FakeContent();
        content.Service.RegisterLoader(new SoundBankLoader(content.Service));
        content.AddFile(path, json);
        return content;
    }

    private const string Bank = """
        {
          "name": "effects",
          "sounds": {
            "coin": { "offset": 0, "duration": 0.25 },
            "jump": { "offset": 1.5, "duration": 0.5 }
          }
        }
        """;

    // The manifest

    [Fact]
    public async Task Manifest_ReadsTheNameAndSegments()
    {
        var manifest = await ReadManifest(Bank);

        Assert.Equal("effects", manifest.Name);
        Assert.Equal(2, manifest.Segments.Count);
        Assert.Equal(1.5, manifest.Segments["jump"].Offset);
        Assert.Equal(0.5, manifest.Segments["jump"].Duration);
    }

    [Fact]
    public async Task Manifest_NamesIgnoreCase()
    {
        var manifest = await ReadManifest("""{ "NAME": "a", "SOUNDS": { "Coin": { "OFFSET": 1, "DURATION": 2 } } }""");

        Assert.Equal("a", manifest.Name);
        Assert.Equal(1, manifest.Segments["cOIN"].Offset);
        Assert.Equal(2, manifest.Segments["cOIN"].Duration);
    }

    [Fact]
    public async Task Manifest_SkipsUnknownMembers_AtEveryLevel()
    {
        var manifest = await ReadManifest("""
            {
              "version": 3,
              "meta": { "nested": [ 1, { "deep": true } ] },
              "sounds": { "coin": { "gain": { "db": 3 }, "offset": 1, "tags": [ "a" ], "duration": 2 } },
              "name": "a",
              "after": null
            }
            """);

        Assert.Equal("a", manifest.Name);
        Assert.Equal(2, manifest.Segments["coin"].Duration);
    }

    [Fact]
    public async Task Manifest_EmptyObject_HasNoNameOrSegments()
    {
        var manifest = await ReadManifest("{}");

        Assert.Null(manifest.Name);
        Assert.Empty(manifest.Segments);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{ "sounds": [] }""")]
    [InlineData("""{ "sounds": { "coin": 1 } }""")]
    [InlineData("""{ "sounds": { "coin": """)]
    public async Task Manifest_Malformed_Throws(
        string json)
    {
        await Assert.ThrowsAnyAsync<JsonException>(() => ReadManifest(json));
    }

    [Fact]
    public async Task Manifest_ARepeatedSegment_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => ReadManifest("""{ "sounds": { "a": { "duration": 1 }, "A": { "duration": 1 } } }"""));
    }

    // The loader

    [Fact]
    public void Loader_RejectsANullContentService()
    {
        Assert.Throws<ArgumentNullException>(() => new SoundBankLoader(null!));
    }

    [Fact]
    public async Task Loader_LoadsTheSoundBesideTheManifest()
    {
        var content = ContentWithBank("Audio/Banks/bank", Bank);

        await content.Service.LoadAsync<SoundBank>("Audio/Banks/bank");

        Assert.Equal(["Audio/Banks/effects"], content.LoadedSounds);
    }

    [Fact]
    public async Task Loader_AtTheContentRoot_LoadsTheSoundThere()
    {
        var content = ContentWithBank("bank", Bank);

        await content.Service.LoadAsync<SoundBank>("bank");

        Assert.Equal(["effects"], content.LoadedSounds);
    }

    [Fact]
    public async Task Loader_NormalizesBackslashesInThePath()
    {
        var content = ContentWithBank("Audio/bank", Bank);

        await content.Service.LoadAsync<SoundBank>("Audio\\bank");

        Assert.Equal(["Audio/effects"], content.LoadedSounds);
    }

    [Fact]
    public async Task Loader_MakesASegmentOfEachSound_InOneBuffer()
    {
        var content = ContentWithBank("bank", Bank);

        var bank = await content.Service.LoadAsync<SoundBank>("bank");

        var jump = bank.GetSegment("jump");
        Assert.Equal(TimeSpan.FromSeconds(1.5), jump.Offset);
        Assert.Equal(TimeSpan.FromSeconds(0.5), jump.Duration);
        Assert.Same(jump.Sound, bank.GetSegment("COIN").Sound);
        Assert.Single(content.LoadedSounds);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "name": "" }""")]
    public async Task Loader_NoSoundName_IsInvalid(
        string json)
    {
        var content = ContentWithBank("bank", json);

        await Assert.ThrowsAsync<InvalidDataException>(() => content.Service.LoadAsync<SoundBank>("bank"));
    }

    [Theory]
    [InlineData("""{ "name": "a", "sounds": { "b": { "offset": 1 } } }""")] // No duration
    [InlineData("""{ "name": "a", "sounds": { "b": { "offset": 1, "duration": 0 } } }""")]
    [InlineData("""{ "name": "a", "sounds": { "b": { "offset": -1, "duration": 1 } } }""")]
    public async Task Loader_ASegmentThatCannotPlay_Fails(
        string json)
    {
        var content = ContentWithBank("bank", json);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => content.Service.LoadAsync<SoundBank>("bank"));
    }

    [Fact]
    public async Task Loader_AMissingSound_Fails()
    {
        var content = ContentWithBank("bank", Bank);
        content.MissingContent = "effects";

        await Assert.ThrowsAsync<FileNotFoundException>(() => content.Service.LoadAsync<SoundBank>("bank"));
    }

    // The bank

    [Fact]
    public async Task Bank_GetSegment_ThrowsForAnUnknownName_NamingIt()
    {
        var content = ContentWithBank("bank", Bank);
        var bank = await content.Service.LoadAsync<SoundBank>("bank");

        var exception = Assert.Throws<KeyNotFoundException>(() => bank.GetSegment("nope"));

        Assert.Contains("nope", exception.Message);
    }

    [Fact]
    public async Task Bank_Dispose_DisposesTheSound_AndForgetsItsSegments()
    {
        var content = ContentWithBank("bank", Bank);
        var bank = await content.Service.LoadAsync<SoundBank>("bank");
        var sound = bank.GetSegment("coin").Sound;
        Assert.False(bank.IsDisposed);

        ((IDisposable)bank).Dispose();

        Assert.True(bank.IsDisposed);
        Assert.True(((IContent)sound).IsDisposed);
        Assert.Throws<KeyNotFoundException>(() => bank.GetSegment("coin"));
    }

    [Fact]
    public void Bank_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new SoundBank(null!, []));
        Assert.Throws<ArgumentNullException>(() => new SoundBank(FakeAudio.CreateSound(), null!));
    }

    // Segments

    [Fact]
    public void Segment_OfAWholeSound_CoversIt()
    {
        var sound = FakeAudio.CreateSound(seconds: 3);

        var segment = sound.AsSegment();

        Assert.Same(sound, segment.Sound);
        Assert.Equal(TimeSpan.Zero, segment.Offset);
        Assert.Equal(TimeSpan.FromSeconds(3), segment.Duration);
    }

    [Fact]
    public void Segment_OfAPart_KeepsItsRange()
    {
        var segment = FakeAudio.CreateSound().AsSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(1), segment.Offset);
        Assert.Equal(TimeSpan.FromSeconds(2), segment.Duration);
    }

    [Fact]
    public void Segment_RejectsNullSoundsAndImpossibleRanges()
    {
        var sound = FakeAudio.CreateSound();
        var second = TimeSpan.FromSeconds(1);

        Assert.Throws<ArgumentNullException>(() => new SoundSegment(null!));
        Assert.Throws<ArgumentNullException>(() => new SoundSegment(null!, TimeSpan.Zero, second));
        Assert.Throws<ArgumentNullException>(() => ((Sound)null!).AsSegment());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoundSegment(sound, -second, second));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoundSegment(sound, TimeSpan.Zero, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoundSegment(sound, TimeSpan.Zero, -second));
    }

    [Fact]
    public void CreateVoice_BakesTheSegmentIn()
    {
        var audio = new FakeAudio();
        var segment = FakeAudio.CreateSound().AsSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        audio.Service.CreateVoice(segment);

        var voice = Assert.Single(audio.Voices);
        Assert.Equal(TimeSpan.FromSeconds(1), voice.Offset);
        Assert.Equal(TimeSpan.FromSeconds(2), voice.Duration);
        Assert.Throws<ArgumentNullException>(() => ((AudioService)null!).CreateVoice(segment));
    }
}
