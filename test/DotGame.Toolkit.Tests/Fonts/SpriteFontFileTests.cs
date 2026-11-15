// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Buffers.Binary;
using DotGame.Toolkit.ContentPipeline.BuildTool.Serialization;
using DotGame.Toolkit.Fonts;
using DotGame.Toolkit.Serialization;
using Xunit;

namespace DotGame.Toolkit.Tests.Fonts;

/// <summary>
/// The <c>.spritefont</c> format, written with the content pipeline's own PBON writer (linked into
/// these tests), read back, and loaded through the content service.
/// </summary>
public sealed class SpriteFontFileTests
{
    // A file's members, as SpriteFontBuilder writes them. A test leaves one out by clearing it.
    private sealed class FontFile
    {
        public int? Version = SpriteFontReader.SupportedVersion;
        public string? Format = "msdf";
        public float? EmSize = 32;
        public float? DistanceRange = 4;
        public float? DistanceRangeMiddle = 0.5f;
        public string[] Pages = ["font_0", "font_1"];
        public int GlyphCount = 2;
        public int KerningCount = 1;
        public byte[] Glyphs = GlyphRecords((65, 0, 12), (0x1F600, 1, 30));
        public byte[]? Kerning = KerningRecord(65, 0x1F600, -2);
        public Action<PbonWriter>? Extra;

        public byte[] Build()
        {
            var stream = new MemoryStream();
            var writer = new PbonWriter(stream);
            writer.StartObject();

            this.Extra?.Invoke(writer);

            if (this.Version is int version)
            {
                writer.Member(1).Write(version);
            }

            if (this.Format is string format)
            {
                writer.Member(2).Write(format);
            }

            if (this.EmSize is float emSize)
            {
                writer.Member(3).Write(emSize);
            }

            if (this.DistanceRange is float range)
            {
                writer.Member(4).Write(range);
            }

            if (this.DistanceRangeMiddle is float middle)
            {
                writer.Member(5).Write(middle);
            }

            writer.Member(6);
            writer.StartObject();
            writer.Member(1).Write(40f);
            writer.Member(2).Write(30f);
            writer.Member(3).Write(-10f);
            writer.Member(4).Write(4f);
            writer.Member(5).Write(2f);
            writer.Member(6).Write(22f);
            writer.EndObject();

            writer.Member(7);
            writer.StartArray();
            foreach (var page in this.Pages)
            {
                writer.StartObject();
                writer.Member(1).Write(page);
                writer.Member(2).Write(512);
                writer.Member(3).Write(256);
                writer.EndObject();
            }

            writer.EndArray();

            writer.Member(8);
            writer.StartArray();
            writer.StartObject();
            writer.Member(1).Write("Test Sans");
            writer.Member(2).Write("TestSans.ttf");
            writer.EndObject();
            writer.EndArray();

            writer.Member(9).Write(this.GlyphCount);
            writer.Member(10).Write(this.KerningCount);
            writer.Member(11).Write(this.Glyphs);
            if (this.Kerning is byte[] kerning)
            {
                writer.Member(12).Write(kerning);
            }

            writer.EndObject();
            return stream.ToArray();
        }
    }

    private static byte[] GlyphRecords(
        params (int CodePoint, int Page, float Advance)[] glyphs)
    {
        var bytes = new byte[glyphs.Length * SpriteFontReader.GlyphRecordSize];
        for (int i = 0; i < glyphs.Length; i++)
        {
            var record = bytes.AsSpan(i * SpriteFontReader.GlyphRecordSize);
            BinaryPrimitives.WriteInt32LittleEndian(record, glyphs[i].CodePoint);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(4), (ushort)glyphs[i].Page);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(6), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(8), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(10), 3);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(12), 4);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(14), glyphs[i].Advance);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(18), 5);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(22), 6);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(26), 7);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(30), 8);
        }

        return bytes;
    }

    private static byte[] KerningRecord(
        int first,
        int second,
        float adjustment)
    {
        var bytes = new byte[SpriteFontReader.KerningRecordSize];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, first);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), second);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8), adjustment);
        return bytes;
    }

    // Reading

    [Fact]
    public void Read_GivesEveryMember()
    {
        var data = SpriteFontReader.Read(new FontFile().Build());

        Assert.Equal(1, data.Version);
        Assert.Equal(GlyphFormat.Msdf, data.GlyphFormat);
        Assert.Equal(32, data.EmSize);
        Assert.Equal(4, data.DistanceRange);
        Assert.Equal(0.5f, data.DistanceRangeMiddle);
        Assert.Equal(40, data.LineHeight);
        Assert.Equal(30, data.Ascender);
        Assert.Equal(-10, data.Descender);
        Assert.Equal(4, data.UnderlinePosition);
        Assert.Equal(2, data.UnderlineThickness);
        Assert.Equal(22, data.CapHeight);
        Assert.Equal(["font_0", "font_1"], data.Pages.Select(p => p.Name));
        Assert.Equal(512, data.Pages[0].Width);
        Assert.Equal(256, data.Pages[0].Height);
        Assert.Equal("Test Sans", Assert.Single(data.Sources).FontFamily);
        Assert.Equal(2, data.GlyphCount);
        Assert.Equal(1, data.KerningCount);
    }

    [Fact]
    public void Read_GlyphAndKerningRecords_ByField()
    {
        var data = SpriteFontReader.Read(new FontFile().Build());

        SpriteFontReader.ReadGlyph(data.Glyphs.Span, 1, out var codePoint, out var page, out var x, out var y, out var width, out var height,
            out var advance, out var left, out var top, out var right, out var bottom);
        Assert.Equal(0x1F600, codePoint);
        Assert.Equal(1, page);
        Assert.Equal((1, 2, 3, 4), (x, y, width, height));
        Assert.Equal(30, advance);
        Assert.Equal((5f, 6f, 7f, 8f), (left, top, right, bottom));

        SpriteFontReader.ReadKerning(data.Kerning.Span, 0, out var first, out var second, out var adjustment);
        Assert.Equal((65, 0x1F600, -2f), (first, second, adjustment));
    }

    [Theory]
    [InlineData("bitmap", (int)GlyphFormat.Bitmap)]
    [InlineData("sdf", (int)GlyphFormat.Sdf)]
    [InlineData("msdf", (int)GlyphFormat.Msdf)]
    [InlineData("mtsdf", (int)GlyphFormat.Mtsdf)]
    public void Read_GlyphFormats(
        string name,
        int format)
    {
        Assert.Equal((GlyphFormat)format, SpriteFontReader.Read(new FontFile { Format = name }.Build()).GlyphFormat);
    }

    [Fact]
    public void Read_SkipsUnknownMembers_WhateverTheyHold()
    {
        var file = new FontFile
        {
            Extra = writer =>
            {
                writer.Member(100).Write("text");
                writer.Member(101).Write(12345);
                writer.Member(102).Write(true);
                writer.Member(103);
                writer.StartObject();
                writer.Member(1);
                writer.StartArray();
                writer.Write(1).Write(2f).Write(false);
                writer.EndArray();
                writer.EndObject();
            },
        };

        Assert.Equal(32, SpriteFontReader.Read(file.Build()).EmSize);
    }

    [Fact]
    public void Read_NewerVersion_AsksForARebuild()
    {
        var file = new FontFile { Version = SpriteFontReader.SupportedVersion + 1 };

        var exception = Assert.Throws<InvalidDataException>(() => SpriteFontReader.Read(file.Build()));

        Assert.Contains("rebuild", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_NoVersion_IsInvalid()
    {
        Assert.Throws<InvalidDataException>(() => SpriteFontReader.Read(new FontFile { Version = null }.Build()));
    }

    [Fact]
    public void Read_NoEmSize_IsInvalid()
    {
        Assert.Throws<InvalidDataException>(() => SpriteFontReader.Read(new FontFile { EmSize = null }.Build()));
    }

    [Fact]
    public void Read_NoPages_IsInvalid()
    {
        Assert.Throws<InvalidDataException>(() => SpriteFontReader.Read(new FontFile { Pages = [] }.Build()));
    }

    [Fact]
    public void Read_UnknownGlyphFormat_IsInvalid()
    {
        Assert.Throws<InvalidDataException>(() => SpriteFontReader.Read(new FontFile { Format = "svg" }.Build()));
    }

    [Fact]
    public void Read_GlyphRecordsThatDontMatchTheCount_AreInvalid()
    {
        Assert.Throws<InvalidDataException>(() => SpriteFontReader.Read(new FontFile { GlyphCount = 3 }.Build()));
    }

    [Fact]
    public void Read_KerningRecordsThatDontMatchTheCount_AreInvalid()
    {
        Assert.Throws<InvalidDataException>(() => SpriteFontReader.Read(new FontFile { KerningCount = 2 }.Build()));
    }

    [Fact]
    public void Read_TruncatedFile_Fails_AtEveryLength()
    {
        var bytes = new FontFile().Build();

        for (int length = 0; length < bytes.Length; length++)
        {
            Assert.ThrowsAny<IOException>(() => SpriteFontReader.Read(bytes.AsMemory(0, length)));
        }
    }

    // Building a font

    [Fact]
    public void Font_FromAFile_HasItsMetricsAndGlyphs()
    {
        var data = SpriteFontReader.Read(new FontFile().Build());
        var font = new SpriteFont(data, [TestTextures.Create(), TestTextures.Create()], TestShaders.Shared);

        Assert.Equal(22, font.CapHeight);
        Assert.Equal(2, font.UnderlineThickness);
        Assert.Equal(4, font.UnderlinePosition);
        Assert.Equal(new System.Numerics.Vector2(512, 256), font.TextureSize);
        Assert.Equal(-2, font.GetKerning(65, 0x1F600));

        var glyph = TestGlyphs.Of(font, 0x1F600)!;
        Assert.Same(font.Pages[1], glyph.Texture);
        Assert.Equal(1, glyph.Page);
        Assert.Equal(30, glyph.Advance);
        Assert.Equal(5, glyph.Left);
        Assert.Equal(6, glyph.Top);
        Assert.Equal(7, glyph.Right);
        Assert.Equal(8, glyph.Bottom);
    }

    [Fact]
    public void Font_KerningRecordsOutOfOrder_StillLookUp()
    {
        var kerning = new byte[SpriteFontReader.KerningRecordSize * 3];
        KerningRecord('Z', 'A', -1).CopyTo(kerning, 0);
        KerningRecord('A', 'Z', -2).CopyTo(kerning, SpriteFontReader.KerningRecordSize);
        KerningRecord('A', 'B', -3).CopyTo(kerning, SpriteFontReader.KerningRecordSize * 2);
        var file = new FontFile { KerningCount = 3, Kerning = kerning };

        var font = new SpriteFont(SpriteFontReader.Read(file.Build()), [TestTextures.Create(), TestTextures.Create()], TestShaders.Shared);

        Assert.Equal(-1, font.GetKerning('Z', 'A'));
        Assert.Equal(-2, font.GetKerning('A', 'Z'));
        Assert.Equal(-3, font.GetKerning('A', 'B'));
        Assert.Equal(0, font.GetKerning('B', 'A'));
    }

    // Loading

    private static FakeContent ContentWithFont(
        string path)
    {
        var content = new FakeContent();
        content.Service.RegisterLoader(new SpriteFontLoader(content.Service));
        content.AddFile(path, new FontFile().Build());
        return content;
    }

    [Fact]
    public void Loader_RejectsANullContentService()
    {
        Assert.Throws<ArgumentNullException>(() => new SpriteFontLoader(null!));
    }

    [Fact]
    public async Task Loader_LoadsPagesBesideTheFont_InOrder()
    {
        var content = ContentWithFont("Fonts/Sans/body");

        var font = await content.Service.LoadAsync<SpriteFont>("Fonts/Sans/body");

        Assert.Equal(["Fonts/Sans/font_0", "Fonts/Sans/font_1"], content.LoadedTextures);
        Assert.Same(content.Textures["Fonts/Sans/font_0"], font.Pages[0]);
        Assert.Same(content.Textures["Fonts/Sans/font_1"], font.Pages[1]);
    }

    [Fact]
    public async Task Loader_AtTheContentRoot_LoadsPagesThere()
    {
        var content = ContentWithFont("body");

        await content.Service.LoadAsync<SpriteFont>("body");

        Assert.Equal(["font_0", "font_1"], content.LoadedTextures);
    }

    [Fact]
    public async Task Loader_NormalizesBackslashesInThePath()
    {
        var content = ContentWithFont("Fonts/body");

        await content.Service.LoadAsync<SpriteFont>("Fonts\\body");

        Assert.Equal(["Fonts/font_0", "Fonts/font_1"], content.LoadedTextures);
    }

    [Fact]
    public async Task Loader_AMissingPage_Fails()
    {
        var content = ContentWithFont("body");
        content.MissingContent = "font_1";

        await Assert.ThrowsAsync<FileNotFoundException>(() => content.Service.LoadAsync<SpriteFont>("body"));
    }

    [Fact]
    public async Task Font_Dispose_DisposesEveryPage_AndIsSafeToRepeat()
    {
        var content = ContentWithFont("body");
        var font = await content.Service.LoadAsync<SpriteFont>("body");

        ((IDisposable)font).Dispose();
        ((IDisposable)font).Dispose();

        Assert.All(font.Pages, page => Assert.True(page.IsDisposed));
    }

    [Fact]
    public async Task Fonts_ShareOneShader_WhichOutlivesThem()
    {
        var content = ContentWithFont("body");
        content.AddFile("title", new FontFile().Build());
        var body = await content.Service.LoadAsync<SpriteFont>("body");
        var title = await content.Service.LoadAsync<SpriteFont>("title");

        Assert.Same(body.Shader, title.Shader);

        ((IDisposable)body).Dispose();

        Assert.False(title.Shader.IsDisposed);
    }

    // PBON itself

    private static PbonReader ReaderOf(
        Action<PbonWriter> write)
    {
        var stream = new MemoryStream();
        write(new PbonWriter(stream));
        return new PbonReader(stream.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(255)]
    [InlineData(65536)]
    [InlineData(int.MaxValue)]
    [InlineData(-1)]
    [InlineData(-128)]
    [InlineData(-129)]
    [InlineData(int.MinValue)]
    public void Pbon_Integers_RoundTrip(
        int value)
    {
        Assert.Equal(value, ReaderOf(w => w.Write(value)).ReadInt32());
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.5f)]
    [InlineData(-1.5f)]
    [InlineData(float.MaxValue)]
    public void Pbon_Floats_RoundTrip(
        float value)
    {
        Assert.Equal(value, ReaderOf(w => w.Write(value)).ReadSingle());
    }

    [Theory]
    [InlineData("")]
    [InlineData("name")]
    [InlineData("naïve \U0001F600")]
    public void Pbon_Strings_RoundTrip(
        string value)
    {
        Assert.Equal(value, ReaderOf(w => w.Write(value)).ReadString());
    }

    [Fact]
    public void Pbon_LongValues_UseLongerLengths()
    {
        var bytes = new byte[100_000];
        bytes[^1] = 7;

        Assert.Equal(bytes, ReaderOf(w => w.Write(bytes)).ReadBytes().ToArray());
    }

    [Fact]
    public void Pbon_Booleans_RoundTrip()
    {
        Assert.True(ReaderOf(w => w.Write(true)).ReadBoolean());
        Assert.False(ReaderOf(w => w.Write(false)).ReadBoolean());
        Assert.Throws<InvalidDataException>(() => ReaderOf(w => w.Write(1)).ReadBoolean());
    }

    [Fact]
    public void Pbon_LargeMemberKeys_RoundTrip()
    {
        var reader = ReaderOf(w => w.StartObject().Member(1000).Write(1).EndObject());

        Assert.True(reader.ReadStartObject());
        Assert.True(reader.TryReadMember(out var key));
        Assert.Equal(1000, key);
        Assert.Equal(1, reader.ReadInt32());
        Assert.False(reader.TryReadMember(out _));
    }

    [Fact]
    public void Pbon_Skip_PassesNestedValues()
    {
        var reader = ReaderOf(w =>
        {
            w.StartArray().StartObject().Member(1).Write("a").Member(2).StartArray().Write(true).EndArray().EndObject().Write(1.5f).EndArray();
            w.Write(42);
        });

        reader.Skip();

        Assert.Equal(42, reader.ReadInt32());
    }

    [Fact]
    public void Pbon_TheWrongStart_IsInvalid()
    {
        Assert.Throws<InvalidDataException>(() => ReaderOf(w => w.StartArray()).ReadStartObject());
        Assert.Throws<InvalidDataException>(() => ReaderOf(w => w.StartObject()).ReadStartArray());
        Assert.Throws<InvalidDataException>(() => ReaderOf(w => w.StartObject()).ReadInt32());
    }
}
