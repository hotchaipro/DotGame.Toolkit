// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DotGame.Toolkit.IO;

namespace DotGame.Toolkit.Audio;

internal sealed class SoundBankManifest
{
    /// <summary>
    /// The base sound file name declared by the manifest (without extension).
    /// Resolved relative to the manifest's content directory by the loader.
    /// </summary>
    public string? Name { get; private set; }

    public Dictionary<string, SoundBankSegment> Segments { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal static async Task<SoundBankManifest> ReadAsync(
        Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var data = await stream.ReadAllBytesAsync().ConfigureAwait(false);
        var reader = new Utf8JsonReader(data.Span);

        var manifest = new SoundBankManifest();

        // Expect start object
        ReadToken(ref reader, JsonTokenType.StartObject);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();

            if (string.Equals(propertyName, "name", StringComparison.OrdinalIgnoreCase))
            {
                reader.Read();
                manifest.Name = reader.GetString();
            }
            else if (string.Equals(propertyName, "sounds", StringComparison.OrdinalIgnoreCase))
            {
                ReadSegments(ref reader, manifest.Segments);
            }
            else
            {
                // Skip unknown property value
                reader.Read();
                reader.Skip();
            }
        }

        return manifest;
    }

    private static void ReadSegments(
        ref Utf8JsonReader reader,
        Dictionary<string, SoundBankSegment> segments)
    {
        ReadToken(ref reader, JsonTokenType.StartObject);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString()
                ?? throw new JsonException("Expected segment name.");

            var segment = ReadSegment(ref reader);
            segments.Add(name, segment);
        }
    }

    private static SoundBankSegment ReadSegment(
        ref Utf8JsonReader reader)
    {
        double offset = 0;
        double duration = 0;

        ReadToken(ref reader, JsonTokenType.StartObject);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();

            reader.Read();

            if (string.Equals(propertyName, "offset", StringComparison.OrdinalIgnoreCase))
            {
                offset = reader.GetDouble();
            }
            else if (string.Equals(propertyName, "duration", StringComparison.OrdinalIgnoreCase))
            {
                duration = reader.GetDouble();
            }
            else
            {
                reader.Skip();
            }
        }

        return new SoundBankSegment(offset, duration);
    }

    private static void ReadToken(
        ref Utf8JsonReader reader,
        JsonTokenType expectedToken)
    {
        if (!reader.Read() || reader.TokenType != expectedToken)
        {
            throw new JsonException(
                $"Expected {expectedToken} but found {reader.TokenType}.");
        }
    }

    internal readonly struct SoundBankSegment(
        double offset,
        double duration)
    {
        public double Offset { get; } = offset;

        public double Duration { get; } = duration;
    }
}
