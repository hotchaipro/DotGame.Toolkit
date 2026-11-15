// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace DotGame.Toolkit.Serialization;

/// <summary>
/// Reads PBON (portable binary object notation) data from memory without reflection,
/// so it is safe for trimming, AOT and WebAssembly.
/// </summary>
/// <remarks>
/// <para>
/// PBON is a compact, self-describing binary form of JSON-like data with integer member keys.
/// The structural tokens are single bytes (<c>{</c> <c>}</c> <c>[</c> <c>]</c>, <c>~</c> for null,
/// <c>t</c> / <c>f</c> for booleans). Member keys and value lengths are packed integers: big-endian
/// base 128 with bit 7 as the continuation flag and bit 6 of the first byte as the sign, so every
/// token decodes as a negative packed integer and never collides with a key or length. Integer
/// values are a length followed by big-endian magnitude bytes, with the sign in the top bit of the
/// first byte (the magnitude one's-complemented when negative). Floats are a length of 4 followed by
/// big-endian IEEE-754 bytes. Strings and byte arrays are a length followed by the bytes (UTF-8).
/// </para>
/// <para>
/// Readers must not depend on member order and must skip unknown members with
/// <see cref="Skip"/>, so formats can add members without breaking older readers.
/// </para>
/// </remarks>
internal sealed class PbonReader
{
    private const int StartObjectToken = ~'{' | 0x40;
    private const int EndObjectToken = ~'}' | 0x40;
    private const int StartArrayToken = ~'[' | 0x40;
    private const int EndArrayToken = ~']' | 0x40;
    private const int NullValueToken = ~'~' | 0x40;
    private const int TrueValueToken = ~'t' | 0x40;
    private const int FalseValueToken = ~'f' | 0x40;

    private readonly ReadOnlyMemory<byte> _data;
    private int _position;

    public PbonReader(
        ReadOnlyMemory<byte> data)
    {
        this._data = data;
    }

    /// <summary>
    /// Reads the start of an object.
    /// </summary>
    /// <returns><c>true</c> if an object starts, or <c>false</c> if the value is null.</returns>
    public bool ReadStartObject()
    {
        var token = this.ReadPackedInt();
        if (token == StartObjectToken)
        {
            return true;
        }

        if (token == NullValueToken)
        {
            return false;
        }

        throw new InvalidDataException("Expected the start of an object.");
    }

    /// <summary>
    /// Reads the key of the next member of the current object, or the end of the object.
    /// </summary>
    /// <returns><c>false</c> at the end of the object.</returns>
    public bool TryReadMember(
        out int key)
    {
        key = this.ReadPackedInt();
        if (key == EndObjectToken)
        {
            key = 0;
            return false;
        }

        if (key < 0)
        {
            throw new InvalidDataException("Expected a member key.");
        }

        return true;
    }

    /// <summary>
    /// Reads the start of an array.
    /// </summary>
    /// <returns><c>true</c> if an array starts, or <c>false</c> if the value is null.</returns>
    public bool ReadStartArray()
    {
        var token = this.ReadPackedInt();
        if (token == StartArrayToken)
        {
            return true;
        }

        if (token == NullValueToken)
        {
            return false;
        }

        throw new InvalidDataException("Expected the start of an array.");
    }

    /// <summary>
    /// Determines whether the current array has another element, consuming the end of the
    /// array when it does not.
    /// </summary>
    public bool TryReadArrayElement()
    {
        var position = this._position;
        if (this.ReadPackedInt() == EndArrayToken)
        {
            return false;
        }

        this._position = position;
        return true;
    }

    public bool ReadBoolean()
    {
        return this.ReadPackedInt() switch
        {
            TrueValueToken => true,
            FalseValueToken => false,
            _ => throw new InvalidDataException("Expected a Boolean value."),
        };
    }

    public int ReadInt32()
    {
        var length = this.ReadLength();
        if (length is < 1 or > 4)
        {
            throw new InvalidDataException("Invalid integer length.");
        }

        var bytes = this.Take(length);
        var negative = (bytes[0] & 0x80) != 0;
        var value = bytes[0] & 0x7F;
        for (int i = 1; i < length; i++)
        {
            value = (value << 8) | bytes[i];
        }

        return negative ? ~value : value;
    }

    public float ReadSingle()
    {
        var length = this.ReadLength();
        var bytes = this.Take(length);
        return length switch
        {
            4 => BinaryPrimitives.ReadSingleBigEndian(bytes),
            8 => (float)BinaryPrimitives.ReadDoubleBigEndian(bytes),
            _ => throw new InvalidDataException("Invalid floating-point length."),
        };
    }

    public string ReadString()
    {
        return Encoding.UTF8.GetString(this.ReadBytes().Span);
    }

    /// <summary>
    /// Reads a byte array value without copying it.
    /// </summary>
    public ReadOnlyMemory<byte> ReadBytes()
    {
        var length = this.ReadLength();
        var start = this._position;
        this.Take(length);
        return this._data.Slice(start, length);
    }

    /// <summary>
    /// Skips the next value, including any nested objects and arrays.
    /// </summary>
    public void Skip()
    {
        var value = this.ReadPackedInt();
        switch (value)
        {
            case StartObjectToken:
                while (this.TryReadMember(out _))
                {
                    this.Skip();
                }
                break;

            case StartArrayToken:
                while (this.TryReadArrayElement())
                {
                    this.Skip();
                }
                break;

            case NullValueToken:
            case TrueValueToken:
            case FalseValueToken:
                break;

            default:
                if (value < 0)
                {
                    throw new InvalidDataException("Unexpected token.");
                }

                this.Take(value);
                break;
        }
    }

    private int ReadLength()
    {
        var length = this.ReadPackedInt();
        if (length < 0)
        {
            throw new InvalidDataException("Expected a primitive value.");
        }

        return length;
    }

    private ReadOnlySpan<byte> Take(
        int count)
    {
        if ((uint)count > (uint)(this._data.Length - this._position))
        {
            throw new EndOfStreamException();
        }

        var span = this._data.Span.Slice(this._position, count);
        this._position += count;
        return span;
    }

    private int ReadPackedInt()
    {
        var span = this._data.Span;
        if (this._position >= span.Length)
        {
            throw new EndOfStreamException();
        }

        var b = span[this._position++];
        var isNegative = (b & 0x40) != 0;
        var value = b & 0x3F;
        while ((b & 0x80) != 0)
        {
            if (this._position >= span.Length)
            {
                throw new EndOfStreamException();
            }

            b = span[this._position++];
            value = (value << 7) | (b & 0x7F);
        }

        return isNegative ? ~value : value;
    }
}
