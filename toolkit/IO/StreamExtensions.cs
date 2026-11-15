// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

namespace DotGame.Toolkit.IO;

internal static class StreamExtensions
{
    /// <summary>
    /// Reads the rest of a content stream into memory with as few copies as possible.
    /// </summary>
    /// <remarks>
    /// A <see cref="MemoryStream"/> with a visible buffer is returned without copying, a seekable
    /// stream is read into an exactly sized array, and any other stream is buffered and copied once.
    /// The result may share the stream's buffer, so it must not be modified.
    /// </remarks>
    public static async Task<ReadOnlyMemory<byte>> ReadAllBytesAsync(
        this Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if ((stream is MemoryStream memoryStream)
            && memoryStream.TryGetBuffer(out var buffer))
        {
            var position = (int)memoryStream.Position;
            memoryStream.Position = memoryStream.Length;
            return buffer.AsMemory(position);
        }

        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining > Array.MaxLength)
            {
                throw new IOException("The content stream is too large to read into memory.");
            }

            var bytes = new byte[remaining];
            await stream.ReadExactlyAsync(bytes).ConfigureAwait(false);
            return bytes;
        }

        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy).ConfigureAwait(false);
        return copy.ToArray();
    }
}
