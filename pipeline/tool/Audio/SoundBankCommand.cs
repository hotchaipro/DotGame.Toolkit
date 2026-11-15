// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DotGame.Framework.ContentPipeline.BuildTool.Audio;

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Audio;

/// <summary>
/// Builds one SoundBankContent bank: concatenates the source sounds (sorted by
/// name) into a single file with silence between them, encodes it with the
/// framework's sound content processor and writes a JSON descriptor of the
/// segment offsets and durations.
/// </summary>
/// <remarks>
/// The build tool reads each source (WAV, FLAC or Ogg Opus), resamples and mixes it to the bank's
/// format, and writes the concatenation as a 16-bit PCM WAV; then the framework's processor command
/// does the final encode, so banks inherit its encoder choice. Durations are snapped to whole samples
/// and each segment starts on a sample boundary at least 100 ms after the previous one ends. A
/// source's duration is its decoded length, and each segment is cut or padded to exactly the samples
/// its entry says, so later sounds never drift early or late.
/// <code>
/// { "name": "&lt;audio file name without extension&gt;",
///   "sounds": { "&lt;segment&gt;": { "offset": seconds, "duration": seconds } } }
/// </code>
/// </remarks>
internal static class SoundBankCommand
{
    public const string Name = "soundbank";
    public const string ErrorCode = "SBK0001";

    private const double MinimumGapSeconds = 0.1;

    public static void Run(CommandLineOptions options)
    {
        string inputList = options.GetPath("input-list");
        string outputFile = options.GetPath("output");
        string audioOutputFile = options.GetPath("audio-output");
        string encodeTemplate = ContentPaths.ReadCommandTemplate(options.GetPath("encode-command-file"));
        string intermediateDir = options.GetPath("intermediate-dir");
        int sampleRate = options.GetInt32("sample-rate", 44100, minimum: 1);
        int channels = options.GetInt32("channels", 2, minimum: 1);
        string inputDisplayDir = options.GetString("input-display-dir");
        string outputDisplayFile = options.GetString("output-display-file");
        string audioDisplayFile = options.GetString("audio-display-file");
        string bankLabel = options.GetString("bank-display-name");
        options.ThrowIfUnrecognized();

        if (channels > 2)
        {
            throw new ContentBuildException($"Option '--channels' must be 1 or 2 (was {channels}).");
        }

        var stopwatch = Stopwatch.StartNew();
        string audioName = Path.GetFileNameWithoutExtension(audioOutputFile);
        if (bankLabel.Length == 0)
        {
            bankLabel = audioName;
        }

        List<string> inputPaths = ContentPaths.ReadPathList(inputList);
        if (inputPaths.Count == 0)
        {
            throw new ContentBuildException("No input audio files specified.");
        }
        ContentPaths.ThrowIfDuplicateNames(inputPaths, "sound");
        inputPaths.Sort((a, b) => ContentPaths.NameComparer.Compare(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b)));
        Directory.CreateDirectory(intermediateDir);

        ContentBuildOutput.Message($"Processing sound bank '{bankLabel}'...");
        ContentBuildOutput.Message($"[{(audioDisplayFile.Length > 0 ? audioDisplayFile : audioOutputFile)}]");

        string intermediateWav = Path.Combine(intermediateDir, audioName + ".concat.wav");
        List<Segment> segments = Concatenate(inputPaths, sampleRate, channels, intermediateWav);

        ExternalProcess.RunShellCommand(
            ExternalProcess.ExpandCommandTemplate(encodeTemplate, intermediateWav, audioOutputFile, intermediateDir),
            "Sound encode command");

        WriteDescriptor(outputFile, audioName, segments);

        string summarySource = inputDisplayDir.Length > 0 ? Path.Combine(inputDisplayDir, "*") : $"{bankLabel} inputs";
        ContentBuildOutput.Message($"{summarySource} -> {(outputDisplayFile.Length > 0 ? outputDisplayFile : outputFile)} [{ContentBuildOutput.FormatElapsed(stopwatch)}s]");
    }

    private static long SampleCount(double seconds, int sampleRate)
    {
        return (long)Math.Round(seconds * sampleRate);
    }

    // Each source in turn: read, converted to the bank's format and written at its segment's offset
    // after silence, so only one source is in memory at a time
    private static List<Segment> Concatenate(List<string> inputPaths, int sampleRate, int channels, string intermediateWav)
    {
        const int WavHeaderSize = 44;
        int bytesPerFrame = channels * sizeof(short);
        var segments = new List<Segment>(inputPaths.Count);
        double offset = 0;
        long frames = 0;
        using (var wav = new FileStream(intermediateWav, FileMode.Create, FileAccess.Write))
        {
            wav.Position = WavHeaderSize;
            for (int i = 0; i < inputPaths.Count; i++)
            {
                string inputPath = inputPaths[i];
                ContentBuildOutput.Message($"[{i + 1}/{inputPaths.Count}] {inputPath}");
                PcmAudio source = AudioReader.Read(inputPath);

                double duration = Math.Round((double)source.Frames / source.SampleRate * sampleRate) / sampleRate;
                if (i > 0)
                {
                    offset = Math.Ceiling((offset + MinimumGapSeconds) * sampleRate) / sampleRate;
                }
                var segment = new Segment(Path.GetFileNameWithoutExtension(inputPath), Math.Round(offset, 6), Math.Round(duration, 6));
                segments.Add(segment);
                offset += duration;

                long start = SampleCount(segment.Offset, sampleRate);
                wav.Write(new byte[(start - frames) * bytesPerFrame]);

                // NOTE: A source resampled to the bank's rate can come out a sample long or short of
                // its entry, so it's forced to the samples the entry says. Otherwise the difference
                // adds up through the bank, and later sounds start early or late.
                PcmAudio prepared = AudioEncoder.Prepare(source, sampleRate, channels).WithFrames((int)SampleCount(segment.Duration, sampleRate));
                short[] samples = prepared.ToInt16();
                var bytes = new byte[samples.Length * sizeof(short)];
                for (int s = 0; s < samples.Length; s++)
                {
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(s * sizeof(short)), samples[s]);
                }
                wav.Write(bytes);
                frames = start + prepared.Frames;
            }

            long dataSize = frames * bytesPerFrame;
            wav.Position = 0;
            using var header = new BinaryWriter(wav, Encoding.ASCII, leaveOpen: true);
            header.Write("RIFF"u8);
            header.Write((uint)(dataSize + WavHeaderSize - 8));
            header.Write("WAVEfmt "u8);
            header.Write(16);
            header.Write((short)1);
            header.Write((short)channels);
            header.Write(sampleRate);
            header.Write(sampleRate * bytesPerFrame);
            header.Write((short)bytesPerFrame);
            header.Write((short)16);
            header.Write("data"u8);
            header.Write((uint)dataSize);
        }

        return segments;
    }

    private static void WriteDescriptor(string path, string audioName, List<Segment> segments)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("name", audioName);
            writer.WriteStartObject("sounds");
            foreach (Segment segment in segments)
            {
                writer.WriteStartObject(segment.Name);
                writer.WriteNumber("offset", segment.Offset);
                writer.WriteNumber("duration", segment.Duration);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        File.WriteAllBytes(path, stream.ToArray());
    }

    private readonly record struct Segment(string Name, double Offset, double Duration);
}
