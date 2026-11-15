// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.Audio;
using DotGame.Toolkit.Fonts;
using DotGame.Toolkit.IO;
using Xunit;

namespace DotGame.Toolkit.Tests;

/// <summary>
/// The small types the rest of the toolkit stands on: thicknesses, code point decoding, and
/// reading content streams.
/// </summary>
public sealed class SupportTypesTests
{
    // Thickness

    [Fact]
    public void ThicknessInt_Constructors_FillTheSides()
    {
        Assert.Equal(new ThicknessInt(3, 3, 3, 3), new ThicknessInt(3));
        Assert.Equal(new ThicknessInt(1, 2, 1, 2), new ThicknessInt(1, 2));
    }

    [Fact]
    public void CharacterCursor_AndSoundSegment_CompareWithDefault()
    {
        Assert.True(default(CharacterCursor) == CharacterCursor.Start);
        Assert.True(new CharacterCursor(1, 1) != CharacterCursor.Start);
        Assert.True(default(SoundSegment) == default);
    }

    [Fact]
    public void ThicknessInt_AddsUpItsSides()
    {
        var thickness = new ThicknessInt(1, 2, 3, 4);

        Assert.Equal(4, thickness.Horizontal);
        Assert.Equal(6, thickness.Vertical);
        Assert.False(thickness == ThicknessInt.Zero);
        Assert.True(default(ThicknessInt) == ThicknessInt.Zero);
    }

    [Fact]
    public void ThicknessInt_With_ReplacesOnlyWhatsGiven()
    {
        var thickness = new ThicknessInt(1, 2, 3, 4);

        Assert.Equal(new ThicknessInt(9, 2, 3, 8), thickness with { Left = 9, Bottom = 8 });
    }

    [Fact]
    public void ThicknessInt_IsAValue()
    {
        var thickness = new ThicknessInt(1, 2, 3, 4);

        Assert.True(thickness == new ThicknessInt(1, 2, 3, 4));
        Assert.True(thickness != new ThicknessInt(1, 2, 3, 5));
        Assert.True(thickness.Equals((object)new ThicknessInt(1, 2, 3, 4)));
        Assert.False(thickness.Equals("(1,2,3,4)"));
        Assert.Equal(new ThicknessInt(1, 2, 3, 4).GetHashCode(), thickness.GetHashCode());
        Assert.Equal("(1,2,3,4)", thickness.ToString());
    }

    [Fact]
    public void ThicknessInt_ConvertsToThickness()
    {
        Thickness thickness = new ThicknessInt(1, 2, 3, 4);

        Assert.Equal(new Thickness(1, 2, 3, 4), thickness);
    }

    [Fact]
    public void Thickness_AddsUpItsSides_AndIsAValue()
    {
        var thickness = new Thickness(1.5f, 2, 3, 4);

        Assert.Equal(4.5f, thickness.Horizontal);
        Assert.Equal(6, thickness.Vertical);
        Assert.False(thickness == Thickness.Zero);
        Assert.True(default(Thickness) == Thickness.Zero);
        Assert.Equal(new Thickness(2, 2, 2, 2), new Thickness(2));
        Assert.Equal(new Thickness(1, 2, 1, 2), new Thickness(1, 2));
        Assert.Equal(new Thickness(9, 2, 3, 4), thickness with { Left = 9 });
        Assert.True(thickness == new Thickness(1.5f, 2, 3, 4));
        Assert.True(thickness != new Thickness(1, 2, 3, 4));
        Assert.Equal(new Thickness(1.5f, 2, 3, 4).GetHashCode(), thickness.GetHashCode());
    }

    // Code points

    private static int[] Decode(
        string text)
    {
        var codePoints = new List<int>();
        foreach (var codePoint in text.AsSpan().EnumerateCodePoints())
        {
            codePoints.Add(codePoint);
        }

        return codePoints.ToArray();
    }

    [Fact]
    public void CodePoints_DecodePairs_AndPassEverythingElse()
    {
        Assert.Empty(Decode(string.Empty));
        Assert.Equal(new[] { 'A', 0x1F600, 'B' }, Decode("A\U0001F600B"));
        Assert.Equal([0x10000, 0x10FFFF], Decode("\U00010000\U0010FFFF"));
    }

    [Fact]
    public void CodePoints_PassUnpairedSurrogates_OneAtATime()
    {
        Assert.Equal([0xD83D], Decode("\uD83D"));
        Assert.Equal([0xDE00], Decode("\uDE00"));
        Assert.Equal(new[] { 0xD83D, 'A' }, Decode("\uD83DA"));
        Assert.Equal([0xDE00, 0xD83D], Decode("\uDE00\uD83D"));
        Assert.Equal([0xD83D, 0x1F600], Decode("\uD83D😀"));
    }

    [Theory]
    [InlineData('A', 1)]
    [InlineData(0xFFFF, 1)]
    [InlineData(0x10000, 2)]
    [InlineData(0x10FFFF, 2)]
    public void CodePoints_Utf16Length(
        int codePoint,
        int length)
    {
        Assert.Equal(length, codePoint.Utf16SequenceLength());
    }

    // Reading streams

    private static readonly byte[] Bytes = [1, 2, 3, 4, 5, 6];

    [Fact]
    public async Task ReadAll_AVisibleMemoryStream_SharesItsBuffer_FromItsPosition()
    {
        var buffer = new byte[10];
        Bytes.CopyTo(buffer, 2);
        var stream = new MemoryStream(buffer, 2, 6, writable: false, publiclyVisible: true);
        stream.Position = 2;

        var memory = await stream.ReadAllBytesAsync();

        Assert.Equal(new byte[] { 3, 4, 5, 6 }, memory.ToArray());
        Assert.Equal(6, stream.Position);
    }

    [Fact]
    public async Task ReadAll_AHiddenMemoryStream_FromItsPosition()
    {
        var stream = new MemoryStream(Bytes, 0, 6, writable: false, publiclyVisible: false);
        stream.Position = 4;

        Assert.Equal(new byte[] { 5, 6 }, (await stream.ReadAllBytesAsync()).ToArray());
    }

    [Fact]
    public async Task ReadAll_AStreamThatCantSeek()
    {
        var stream = new UnseekableStream(new MemoryStream(Bytes));

        Assert.Equal(Bytes, (await stream.ReadAllBytesAsync()).ToArray());
    }

    [Fact]
    public async Task ReadAll_AnEmptyStream_IsEmpty()
    {
        Assert.True((await new MemoryStream().ReadAllBytesAsync()).IsEmpty);
        Assert.True((await new UnseekableStream(new MemoryStream()).ReadAllBytesAsync()).IsEmpty);
    }

    [Fact]
    public async Task ReadAll_RejectsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((Stream)null!).ReadAllBytesAsync());
    }

    private sealed class UnseekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
