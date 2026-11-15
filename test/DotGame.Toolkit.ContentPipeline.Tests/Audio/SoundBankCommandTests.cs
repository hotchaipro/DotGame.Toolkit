// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using DotGame.ContentPipeline.Testing;
using DotGame.Framework.ContentPipeline.BuildTool;
using DotGame.Framework.ContentPipeline.BuildTool.Audio;
using DotGame.Toolkit.ContentPipeline.BuildTool.Audio;
using Xunit;

namespace DotGame.Toolkit.ContentPipeline.Tests.Audio;

/// <summary>
/// The sound bank command in process, with real sources and the fake tool as the encoder.
/// </summary>
public sealed class SoundBankCommandTests : IDisposable
{
    private const double Gap = 0.1;
    private readonly TestDirectory _directory = new();
    private readonly List<string> _sounds = [];

    public void Dispose() => this._directory.Dispose();

    private string ConcatenatedWav => this._directory.Path_("obj", "bank_0.concat.wav");

    // A noise burst of the duration, as a WAV (or FLAC, by the name's extension) at that rate
    private PcmAudio Sound(string name, double duration, int sampleRate = 44100, int channels = 2)
    {
        string path = this._directory.Path_("src", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PcmAudio audio = Tones.Noise(sampleRate, channels, (int)Math.Round(duration * sampleRate), seed: this._sounds.Count + 1);
        if (path.EndsWith(".flac", StringComparison.Ordinal))
        {
            FlacEncoder.Write(path, audio);
        }
        else
        {
            WavWriter.WriteInt16(path, audio);
        }

        this._sounds.Add(path);
        return audio;
    }

    private JsonElement Build(int sampleRate = 44100, int channels = 2)
    {
        string list = this._directory.WriteFile("obj/bank.inputs.txt", string.Join(';', this._sounds));
        string template = this._directory.WriteFile("obj/bank.encode.cmd", $"\"{Paths.FakeTool}\" sound --input \"{{InputFile}}\" --output \"{{OutputFile}}\"");
        string output = this._directory.Path_("out", "bank.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        SoundBankCommand.Run(CommandLineOptions.Parse([
            "--input-list", list,
            "--output", output,
            "--audio-output", this._directory.Path_("out", "bank_0.wav"),
            "--encode-command-file", template,
            "--intermediate-dir", this._directory.Path_("obj"),
            "--sample-rate", sampleRate.ToString(CultureInfo.InvariantCulture),
            "--channels", channels.ToString(CultureInfo.InvariantCulture),
        ]));
        return JsonElement.Parse(File.ReadAllText(output));
    }

    private static (double Offset, double Duration) Segment(JsonElement bank, string name)
    {
        JsonElement segment = bank.GetProperty("sounds").GetProperty(name);
        return (segment.GetProperty("offset").GetDouble(), segment.GetProperty("duration").GetDouble());
    }

    [Fact]
    public void Durations_AreEachSourcesDecodedLength()
    {
        this.Sound("a.wav", 0.5);
        this.Sound("b.flac", 0.2, sampleRate: 22050, channels: 1);

        JsonElement bank = this.Build();

        Assert.Equal(0.5, Segment(bank, "a").Duration);
        Assert.Equal(0.2, Segment(bank, "b").Duration);
        Assert.Empty(this._directory.ToolCalls().FindAll(call => call.Kind == "ffmpeg"));
    }

    [Fact]
    public void Descriptor_NamesTheAudioFile_WithoutItsExtension()
    {
        this.Sound("a.wav", 0.1);

        Assert.Equal("bank_0", this.Build().GetProperty("name").GetString());
    }

    [Fact]
    public void Segments_AreSortedByName_WhateverTheInputOrder()
    {
        this.Sound("zebra.wav", 0.1);
        this.Sound("Apple.wav", 0.1);
        this.Sound("mango.wav", 0.1);

        JsonElement bank = this.Build();

        Assert.Equal(["Apple", "mango", "zebra"], bank.GetProperty("sounds").EnumerateObject().Select(sound => sound.Name));
        Assert.Equal(0, Segment(bank, "Apple").Offset);
        Assert.True(Segment(bank, "mango").Offset < Segment(bank, "zebra").Offset);
    }

    [Fact]
    public void Offsets_LeaveAtLeastTheGap_OnASampleBoundary()
    {
        this.Sound("a.wav", 1);
        this.Sound("b.wav", 0.5);
        this.Sound("c.wav", 0.25);

        JsonElement bank = this.Build();

        (double aOffset, double aDuration) = Segment(bank, "a");
        (double bOffset, double bDuration) = Segment(bank, "b");
        (double cOffset, _) = Segment(bank, "c");
        Assert.Equal(0, aOffset);
        Assert.Equal(1, aDuration);
        Assert.InRange(bOffset - (aOffset + aDuration), Gap - 1e-6, Gap + (2.0 / 44100));
        Assert.InRange(cOffset - (bOffset + bDuration), Gap - 1e-6, Gap + (2.0 / 44100));

        // Whole samples, to the descriptor's six decimals
        Assert.InRange(Math.Abs((bOffset * 44100) - Math.Round(bOffset * 44100)), 0, 0.05);
        Assert.InRange(Math.Abs((cOffset * 44100) - Math.Round(cOffset * 44100)), 0, 0.05);
    }

    [Fact]
    public void Durations_SnapToWholeSamples()
    {
        this.Sound("a.wav", 0.12345);

        double duration = Segment(this.Build(sampleRate: 1000), "a").Duration;

        Assert.Equal(0.123, duration);
    }

    [Fact]
    public void Offsets_FollowTheSampleRate()
    {
        this.Sound("a.wav", 1);
        this.Sound("b.wav", 1);

        double offset = Segment(this.Build(sampleRate: 1000), "b").Offset;

        Assert.Equal(1.1, offset, 6);
    }

    [Fact]
    public void Concatenation_PutsEachSoundAtItsOffset_WithSilenceBetween()
    {
        PcmAudio a = this.Sound("a.wav", 0.2);
        PcmAudio b = this.Sound("b.wav", 0.3);

        JsonElement bank = this.Build();

        PcmAudio concatenated = AudioReader.Read(this.ConcatenatedWav);
        int bStart = (int)Math.Round(Segment(bank, "b").Offset * 44100);
        Assert.Equal(bStart + b.Frames, concatenated.Frames);
        Assert.Equal(a.Samples, concatenated.Samples.AsSpan(0, a.Samples.Length).ToArray());
        Assert.All(concatenated.Samples[a.Samples.Length..(bStart * 2)], sample => Assert.Equal(0, sample));
        Assert.Equal(b.Samples, concatenated.Samples.AsSpan(bStart * 2).ToArray());
    }

    [Fact]
    public void Concatenation_ConvertsEachSourceToTheBanksFormat()
    {
        this.Sound("a.flac", 0.5, sampleRate: 22050, channels: 1);
        this.Sound("b.wav", 0.25, sampleRate: 48000, channels: 2);

        JsonElement bank = this.Build(sampleRate: 44100, channels: 2);

        PcmAudio concatenated = AudioReader.Read(this.ConcatenatedWav);
        Assert.Equal(44100, concatenated.SampleRate);
        Assert.Equal(2, concatenated.Channels);
        Assert.Equal((int)Math.Round(Segment(bank, "b").Offset * 44100) + (int)Math.Round(0.25 * 44100), concatenated.Frames);
    }

    [Fact]
    public void Channels_ChooseTheLayout()
    {
        this.Sound("a.wav", 0.1);

        this.Build(channels: 1);

        Assert.Equal(1, AudioReader.Read(this.ConcatenatedWav).Channels);
    }

    [Fact]
    public void Channels_AboveTwo_AreRefused()
    {
        this.Sound("a.wav", 0.1);

        Assert.Throws<ContentBuildException>(() => this.Build(channels: 6));
    }

    [Fact]
    public void Encoder_GetsTheConcatenatedFile_AndWritesTheAudio()
    {
        this.Sound("a.wav", 0.1);

        this.Build();

        ToolCall encode = Assert.Single(this._directory.ToolCalls(), call => call.Kind == "sound");
        Assert.Equal("bank_0.concat.wav", encode.Input);
        Assert.True(File.Exists(this._directory.Path_("out", "bank_0.wav")));
    }

    [Fact]
    public void FailedEncoder_LeavesNoDescriptor()
    {
        this.Sound("a.wav", 0.1);
        this._directory.SetToolConfig("fail.sound", "partial");

        Assert.Throws<ContentBuildException>(() => this.Build());

        Assert.False(File.Exists(this._directory.Path_("out", "bank.json")));
    }

    [Fact]
    public void UnreadableSource_Fails_NamingIt()
    {
        this._sounds.Add(this._directory.WriteFile(Path.Combine("src", "a.wav"), "audio"));

        var error = Assert.Throws<ContentBuildException>(() => this.Build());

        Assert.Contains("a.wav", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(this._directory.Path_("out", "bank.json")));
    }

    [Fact]
    public void Mp3Source_Fails_NamingTheFormat()
    {
        string path = this._directory.Path_("src", "a.mp3");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0x49, 0x44, 0x33, 4, 0, 0, 0, 0, 0, 0]);
        this._sounds.Add(path);

        var error = Assert.Throws<ContentBuildException>(() => this.Build());

        Assert.Contains("MP3", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateNames_Fail()
    {
        this.Sound("hit.wav", 0.1);
        this.Sound(Path.Combine("other", "HIT.flac"), 0.1);

        Assert.Throws<ContentBuildException>(() => this.Build());
    }

    [Fact]
    public void MissingSound_FailsNamingIt()
    {
        this._sounds.Add(this._directory.Path_("src", "none.wav"));

        var error = Assert.Throws<ContentBuildException>(() => this.Build());

        Assert.Contains("none.wav", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoInputs_Fail()
    {
        Assert.Throws<ContentBuildException>(() => this.Build());
    }

    [Fact]
    public void NonAsciiNames_AreWrittenAsTheyAre()
    {
        this.Sound("été.wav", 0.1);

        this.Build();

        Assert.Contains("\"été\"", File.ReadAllText(this._directory.Path_("out", "bank.json")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait(Traits.Category, Traits.Slow)]
    public void LargeBank_Builds()
    {
        string name = new('n', 100);
        for (int i = 0; i < 300; i++)
        {
            this.Sound($"{name}{i}.wav", 0.1);
        }

        Assert.Equal(300, this.Build().GetProperty("sounds").EnumerateObject().Count());
    }
}
