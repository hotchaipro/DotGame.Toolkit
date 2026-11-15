// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Buffers.Binary;
using System.Text;

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Serialization;

// Minimal Portable Binary Object Notation (PBON) writer.  See https://pbon.info.
//   object  = '{' (memberKey value)* '}'     memberKey = positive packed int
//   array   = '[' value* ']'
//   value   = object | array | 't' | 'f' | '~' (null) | length-prefixed bytes
// Packed ints are big-endian base-128 with bit 6 of the first byte as the sign; tokens are
// negative packed ints whose single byte equals the token character.  Integers are stored
// as length-prefixed big-endian two's-complement-free sign/magnitude (sign in the top bit,
// magnitude one's-complemented when negative); floats as 4 big-endian IEEE-754 bytes.
internal sealed class PbonWriter
{
    private readonly Stream _stream;

    public PbonWriter(
        Stream stream)
    {
        this._stream = stream;
    }

    public PbonWriter StartObject()
    {
        this._stream.WriteByte((byte)'{');
        return this;
    }

    public PbonWriter EndObject()
    {
        this._stream.WriteByte((byte)'}');
        return this;
    }

    public PbonWriter StartArray()
    {
        this._stream.WriteByte((byte)'[');
        return this;
    }

    public PbonWriter EndArray()
    {
        this._stream.WriteByte((byte)']');
        return this;
    }

    public PbonWriter Member(
        int key)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(key);

        this.WritePacked(key);
        return this;
    }

    public PbonWriter Write(
        bool value)
    {
        this._stream.WriteByte(value ? (byte)'t' : (byte)'f');
        return this;
    }

    public PbonWriter Write(
        int value)
    {
        byte sign = 0;
        if (value < 0)
        {
            sign = 0x80;
            value = ~value;
        }
        int length = value <= 0x7F ? 1 : value <= 0x7FFF ? 2 : value <= 0x7FFFFF ? 3 : 4;
        this.WritePacked(length);
        for (int i = length - 1; i >= 0; i--)
        {
            byte b = (byte)(value >> (i * 8));
            this._stream.WriteByte(i == length - 1 ? (byte)(b | sign) : b);
        }
        return this;
    }

    public PbonWriter Write(
        float value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, BitConverter.SingleToInt32Bits(value));
        this.WritePacked(4);
        this._stream.Write(bytes);
        return this;
    }

    public PbonWriter Write(
        string value)
    {
        return this.Write(Encoding.UTF8.GetBytes(value));
    }

    public PbonWriter Write(
        ReadOnlySpan<byte> value)
    {
        this.WritePacked(value.Length);
        this._stream.Write(value);
        return this;
    }

    private void WritePacked(
        int value)
    {
        // Non-negative only (lengths and member keys); sign bit 6 stays clear.
        if (value <= 0x3F)
        {
            this._stream.WriteByte((byte)value);
        }
        else if (value <= 0x1FFF)
        {
            this._stream.WriteByte((byte)(0x80 | (value >> 7)));
            this._stream.WriteByte((byte)(value & 0x7F));
        }
        else if (value <= 0xFFFFF)
        {
            this._stream.WriteByte((byte)(0x80 | (value >> 14)));
            this._stream.WriteByte((byte)(0x80 | ((value >> 7) & 0x7F)));
            this._stream.WriteByte((byte)(value & 0x7F));
        }
        else if (value <= 0x7FFFFFF)
        {
            this._stream.WriteByte((byte)(0x80 | (value >> 21)));
            this._stream.WriteByte((byte)(0x80 | ((value >> 14) & 0x7F)));
            this._stream.WriteByte((byte)(0x80 | ((value >> 7) & 0x7F)));
            this._stream.WriteByte((byte)(value & 0x7F));
        }
        else
        {
            this._stream.WriteByte((byte)(0x80 | (value >> 28)));
            this._stream.WriteByte((byte)(0x80 | ((value >> 21) & 0x7F)));
            this._stream.WriteByte((byte)(0x80 | ((value >> 14) & 0x7F)));
            this._stream.WriteByte((byte)(0x80 | ((value >> 7) & 0x7F)));
            this._stream.WriteByte((byte)(value & 0x7F));
        }
    }
}
