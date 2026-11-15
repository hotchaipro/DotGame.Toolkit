// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.ContentPipeline.BuildTool.Serialization;
using DotGame.Toolkit.Serialization;
using Xunit;

namespace DotGame.Toolkit.ContentPipeline.Tests.Serialization;

/// <summary>
/// The build tool's PBON writer against the runtime's reader.
/// </summary>
public sealed class PbonRoundTripTests
{
    private static PbonReader Write(Action<PbonWriter> write)
    {
        var stream = new MemoryStream();
        write(new PbonWriter(stream));
        return new PbonReader(stream.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(-128)]
    [InlineData(-129)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(32767)]
    [InlineData(32768)]
    [InlineData(-32768)]
    [InlineData(-32769)]
    [InlineData(8388607)]
    [InlineData(8388608)]
    [InlineData(-8388608)]
    [InlineData(-8388609)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Int32_RoundTrips(int value)
    {
        Assert.Equal(value, Write(writer => writer.Write(value)).ReadInt32());
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.5f)]
    [InlineData(-3.25f)]
    [InlineData(float.Epsilon)]
    [InlineData(float.MaxValue)]
    public void Single_RoundTrips(float value)
    {
        Assert.Equal(value, Write(writer => writer.Write(value)).ReadSingle());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Boolean_RoundTrips(bool value)
    {
        Assert.Equal(value, Write(writer => writer.Write(value)).ReadBoolean());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Inter Medium")]
    [InlineData("été 日本語 🎮")]
    public void String_RoundTrips(string value)
    {
        Assert.Equal(value, Write(writer => writer.Write(value)).ReadString());
    }

    // A length crosses each size of packed integer: 6, 13, 20 and 27 bits
    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(8191)]
    [InlineData(8192)]
    [InlineData(1048575)]
    [InlineData(1048576)]
    public void Bytes_RoundTrip_AtEveryLengthEncoding(int length)
    {
        var value = new byte[length];
        for (int i = 0; i < length; i++)
        {
            value[i] = (byte)(i * 31);
        }

        Assert.Equal(value, Write(writer => writer.Write((ReadOnlySpan<byte>)value)).ReadBytes().ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(8191)]
    [InlineData(8192)]
    [InlineData(1048576)]
    [InlineData(134217728)]
    public void MemberKeys_RoundTrip_WithoutCollidingWithTokens(int key)
    {
        PbonReader reader = Write(writer => writer.StartObject().Member(key).Write(7).EndObject());

        Assert.True(reader.ReadStartObject());
        Assert.True(reader.TryReadMember(out int read));
        Assert.Equal(key, read);
        Assert.Equal(7, reader.ReadInt32());
        Assert.False(reader.TryReadMember(out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Member_RefusesAKeyThatIsntPositive(int key)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Write(writer => writer.Member(key)));
    }

    [Fact]
    public void NestedObjectsAndArrays_RoundTrip()
    {
        PbonReader reader = Write(writer =>
        {
            writer.StartObject();
            writer.Member(1).StartArray().Write("a").Write("b").EndArray();
            writer.Member(2).StartObject().Member(3).Write(true).EndObject();
            writer.EndObject();
        });

        Assert.True(reader.ReadStartObject());
        Assert.True(reader.TryReadMember(out int first));
        Assert.Equal(1, first);
        Assert.True(reader.ReadStartArray());
        Assert.True(reader.TryReadArrayElement());
        Assert.Equal("a", reader.ReadString());
        Assert.True(reader.TryReadArrayElement());
        Assert.Equal("b", reader.ReadString());
        Assert.False(reader.TryReadArrayElement());
        Assert.True(reader.TryReadMember(out int second));
        Assert.Equal(2, second);
        Assert.True(reader.ReadStartObject());
        Assert.True(reader.TryReadMember(out _));
        Assert.True(reader.ReadBoolean());
        Assert.False(reader.TryReadMember(out _));
        Assert.False(reader.TryReadMember(out _));
    }

    [Fact]
    public void Skip_PassesAnyValueWhole()
    {
        PbonReader reader = Write(writer =>
        {
            writer.StartObject();
            writer.Member(1).StartObject().Member(2).StartArray().Write(1).Write(2.5f).Write("x").Write(true).EndArray().EndObject();
            writer.Member(9).Write(42);
            writer.EndObject();
        });

        reader.ReadStartObject();
        reader.TryReadMember(out _);
        reader.Skip();

        Assert.True(reader.TryReadMember(out int key));
        Assert.Equal(9, key);
        Assert.Equal(42, reader.ReadInt32());
    }
}
