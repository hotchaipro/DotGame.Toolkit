// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

// glyphgen - renders font glyphs for the SpriteFontContent pipeline.
//
// glyphgen only renders glyph outlines to pixels. Everything read from the font's tables
// (metrics, names, the character map, advances, kerning) is read by the caller
// (build/Fonts/FontFile.cs).
//
// Usage:
//   glyphgen --font <file> --glyphs <file> --output <file>
//            [--format msdf|mtsdf|sdf|bitmap] [--size <px per em>]
//            [--range-lower <px>] [--range-upper <px>] [--threads <n>]
//
// --glyphs is a text file of whitespace-separated decimal glyph indices, which are rendered
// sorted and without duplicates.
//
// Output (little-endian):
//   char[4] "GLGN", u32 version (2)
//   u32 glyphCount
//   per glyph:
//     u32 glyphIndex
//     f64 advance (em units; the hinted advance of a bitmap glyph, which only rendering
//         gives; zero for distance fields, whose advance is the font's)
//     f64 planeLeft, planeBottom, planeRight, planeTop (em units, y up: where the pixels sit
//         relative to the glyph origin)
//     u32 width, u32 height, u32 channels
//     u8[width * height * channels] pixels, rows top to bottom

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <thread>
#include <vector>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#endif

#include <ft2build.h>
#include FT_FREETYPE_H
#include FT_TRUETYPE_TABLES_H

#include "msdfgen.h"
#include "ext/import-font.h"
#include "clipper2/clipper.h"

namespace {

enum class GlyphFormat { Bitmap, Sdf, Msdf, Mtsdf };

struct Options {
    std::string fontPath;
    std::string glyphsPath;
    std::string outputPath;
    GlyphFormat format = GlyphFormat::Msdf;
    double size = 32.0;
    double rangeLower = -2.0;
    double rangeUpper = 2.0;
    unsigned threads = 0;
};

struct Glyph {
    uint32_t glyphIndex = 0;
    double advance = 0.0;
    double left = 0.0, bottom = 0.0, right = 0.0, top = 0.0;
    uint32_t width = 0, height = 0, channels = 0;
    std::vector<uint8_t> pixels;
    msdfgen::Shape shape;
    int originX = 0, originY = 0;
};

const double EdgeColoringAngleThreshold = 3.0;
const double OuterPixelPadding = 0.5;

[[noreturn]] void fail(const std::string &message) {
    std::fprintf(stderr, "glyphgen: error: %s\n", message.c_str());
    std::exit(1);
}

// Opens a file from a UTF-8 path (Windows needs the wide-character API for non-ANSI paths).
FILE *openFile(const std::string &path, const char *mode) {
#ifdef _WIN32
    auto widen = [](const std::string &value) {
        int length = MultiByteToWideChar(CP_UTF8, 0, value.c_str(), -1, nullptr, 0);
        std::wstring result(length > 0 ? length - 1 : 0, L'\0');
        if (length > 1)
            MultiByteToWideChar(CP_UTF8, 0, value.c_str(), -1, &result[0], length);
        return result;
    };
    return _wfopen(widen(path).c_str(), widen(mode).c_str());
#else
    return std::fopen(path.c_str(), mode);
#endif
}

std::vector<std::string> getArguments(int argc, char **argv) {
    std::vector<std::string> arguments;
#ifdef _WIN32
    (void) argc;
    (void) argv;
    int count = 0;
    LPWSTR *wide = CommandLineToArgvW(GetCommandLineW(), &count);
    for (int i = 0; wide && i < count; ++i) {
        int length = WideCharToMultiByte(CP_UTF8, 0, wide[i], -1, nullptr, 0, nullptr, nullptr);
        std::string value(length > 0 ? length - 1 : 0, '\0');
        if (length > 1)
            WideCharToMultiByte(CP_UTF8, 0, wide[i], -1, &value[0], length, nullptr, nullptr);
        arguments.push_back(value);
    }
    LocalFree(wide);
#else
    for (int i = 0; i < argc; ++i)
        arguments.push_back(argv[i]);
#endif
    return arguments;
}

std::vector<uint8_t> readFile(const std::string &path) {
    FILE *file = openFile(path, "rb");
    if (!file)
        fail("cannot open file: " + path);
    std::vector<uint8_t> data;
    uint8_t buffer[65536];
    size_t count;
    while ((count = std::fread(buffer, 1, sizeof buffer, file)) > 0)
        data.insert(data.end(), buffer, buffer + count);
    std::fclose(file);
    return data;
}

bool parseFormat(const std::string &value, GlyphFormat &format) {
    if (value == "bitmap") format = GlyphFormat::Bitmap;
    else if (value == "sdf") format = GlyphFormat::Sdf;
    else if (value == "msdf") format = GlyphFormat::Msdf;
    else if (value == "mtsdf") format = GlyphFormat::Mtsdf;
    else return false;
    return true;
}

double parseDouble(const std::string &name, const std::string &text) {
    const char *value = text.c_str();
    char *end = nullptr;
    double result = std::strtod(value, &end);
    if (end == value || *end != '\0' || !std::isfinite(result))
        fail("invalid value for " + name + ": " + text);
    return result;
}

Options parseArguments(const std::vector<std::string> &args) {
    Options options;
    for (size_t i = 1; i < args.size(); ++i) {
        const std::string &arg = args[i];
        if (i + 1 >= args.size())
            fail("missing value for " + arg);
        const std::string &value = args[++i];
        if (arg == "--font") options.fontPath = value;
        else if (arg == "--glyphs") options.glyphsPath = value;
        else if (arg == "--output") options.outputPath = value;
        else if (arg == "--format") {
            if (!parseFormat(value, options.format))
                fail("unknown format: " + value);
        }
        else if (arg == "--size") options.size = parseDouble("--size", value);
        else if (arg == "--range-lower") options.rangeLower = parseDouble("--range-lower", value);
        else if (arg == "--range-upper") options.rangeUpper = parseDouble("--range-upper", value);
        else if (arg == "--threads") options.threads = static_cast<unsigned>(parseDouble("--threads", value));
        else fail("unknown argument: " + arg);
    }
    if (options.fontPath.empty() || options.glyphsPath.empty() || options.outputPath.empty())
        fail("--font, --glyphs and --output are required");
    if (options.size <= 0.0)
        fail("--size must be positive");
    if (options.format != GlyphFormat::Bitmap && !(options.rangeLower < 0.0 && options.rangeUpper > 0.0))
        fail("distance range must satisfy range-lower < 0 < range-upper");
    return options;
}

std::vector<uint32_t> readGlyphIndices(const std::string &path) {
    std::vector<uint8_t> text = readFile(path);
    std::vector<uint32_t> glyphIndices;
    uint32_t value = 0;
    bool inNumber = false;
    for (uint8_t c : text) {
        if (c >= '0' && c <= '9') {
            value = value * 10 + (c - '0');
            inNumber = true;
        }
        else if (c == ' ' || c == '\t' || c == '\r' || c == '\n') {
            if (inNumber)
                glyphIndices.push_back(value);
            value = 0;
            inNumber = false;
        }
        else
            fail("invalid character in glyph index file: " + path);
    }
    if (inNumber)
        glyphIndices.push_back(value);
    std::sort(glyphIndices.begin(), glyphIndices.end());
    glyphIndices.erase(std::unique(glyphIndices.begin(), glyphIndices.end()), glyphIndices.end());
    return glyphIndices;
}

uint8_t toByte(float value) {
    return static_cast<uint8_t>(std::lround(std::min(std::max(value, 0.0f), 1.0f) * 255.0f));
}

template <int N>
void storePixels(Glyph &glyph, const msdfgen::Bitmap<float, N> &bitmap) {
    glyph.channels = N;
    glyph.pixels.resize(static_cast<size_t>(glyph.width) * glyph.height * N);
    uint8_t *out = glyph.pixels.data();
    // msdfgen bitmaps are y-up; the output is top to bottom.
    for (int y = static_cast<int>(glyph.height) - 1; y >= 0; --y)
        for (int x = 0; x < static_cast<int>(glyph.width); ++x) {
            const float *pixel = bitmap(x, y);
            for (int c = 0; c < N; ++c)
                *out++ = toByte(pixel[c]);
        }
}

void generateDistanceField(Glyph &glyph, const Options &options) {
    if (glyph.width == 0 || glyph.height == 0)
        return;

    const double scale = options.size;
    msdfgen::Projection projection(msdfgen::Vector2(scale), msdfgen::Vector2(-glyph.originX / scale, -glyph.originY / scale));
    msdfgen::Range range(options.rangeLower / scale, options.rangeUpper / scale);
    msdfgen::SDFTransformation transformation(projection, msdfgen::DistanceMapping(range));
    const float zeroValue = static_cast<float>(-options.rangeLower / (options.rangeUpper - options.rangeLower));
    const int w = static_cast<int>(glyph.width), h = static_cast<int>(glyph.height);

    msdfgen::MSDFGeneratorConfig config;
    config.errorCorrection.mode = msdfgen::ErrorCorrectionConfig::DISABLED;
    msdfgen::MSDFGeneratorConfig correctionConfig;
    correctionConfig.errorCorrection.mode = msdfgen::ErrorCorrectionConfig::EDGE_PRIORITY;
    correctionConfig.errorCorrection.distanceCheckMode = msdfgen::ErrorCorrectionConfig::DO_NOT_CHECK_DISTANCE;

    switch (options.format) {
        case GlyphFormat::Sdf: {
            msdfgen::Bitmap<float, 1> bitmap(w, h);
            msdfgen::generateSDF(bitmap, glyph.shape, transformation);
            msdfgen::distanceSignCorrection(bitmap, glyph.shape, projection, zeroValue, msdfgen::FILL_NONZERO);
            storePixels(glyph, bitmap);
            break;
        }
        case GlyphFormat::Msdf: {
            msdfgen::Bitmap<float, 3> bitmap(w, h);
            msdfgen::generateMSDF(bitmap, glyph.shape, transformation, config);
            msdfgen::distanceSignCorrection(bitmap, glyph.shape, projection, zeroValue, msdfgen::FILL_NONZERO);
            msdfgen::msdfErrorCorrection(bitmap, glyph.shape, transformation, correctionConfig);
            storePixels(glyph, bitmap);
            break;
        }
        case GlyphFormat::Mtsdf: {
            msdfgen::Bitmap<float, 4> bitmap(w, h);
            msdfgen::generateMTSDF(bitmap, glyph.shape, transformation, config);
            msdfgen::distanceSignCorrection(bitmap, glyph.shape, projection, zeroValue, msdfgen::FILL_NONZERO);
            msdfgen::msdfErrorCorrection(bitmap, glyph.shape, transformation, correctionConfig);
            storePixels(glyph, bitmap);
            break;
        }
        default:
            break;
    }
}

// Maximum distance (em) between a flattened curve and the true curve. Small enough
// that adjacent segments turn far less than the edge coloring corner threshold.
constexpr double FlatteningTolerance = 2e-5;

// Clipper works on integer coordinates; em units are scaled to this many steps per em.
constexpr double ClipperScale = 1e7;

void flattenEdge(const msdfgen::EdgeSegment &edge, Clipper2Lib::Path64 &path) {
    const msdfgen::Point2 *p = edge.controlPoints();
    double curvature = 0.0;
    switch (edge.type()) {
        case msdfgen::QuadraticSegment::EDGE_TYPE:
            curvature = 2.0 * (p[0] - 2.0 * p[1] + p[2]).length();
            break;
        case msdfgen::CubicSegment::EDGE_TYPE:
            curvature = 6.0 * std::max((p[0] - 2.0 * p[1] + p[2]).length(), (p[1] - 2.0 * p[2] + p[3]).length());
            break;
        default:
            break;
    }
    // Chord error of n uniform steps is at most curvature / (8 n^2).
    const int steps = curvature > 0.0
        ? std::clamp(static_cast<int>(std::ceil(std::sqrt(curvature / (8.0 * FlatteningTolerance)))), 1, 1024)
        : 1;
    for (int i = 0; i < steps; ++i) {
        const msdfgen::Point2 point = edge.point(static_cast<double>(i) / steps);
        const Clipper2Lib::Point64 scaled(std::llround(point.x * ClipperScale), std::llround(point.y * ClipperScale));
        if (path.empty() || path.back() != scaled)
            path.push_back(scaled);
    }
}

// Line edges shorter than this (em) that turn back on their neighbors are removed.
// Some fonts' outlines step a few units backward where a curve meets a stem (a "u" whose
// curve ends a unit above where its stem line starts). A distance field keeps such a
// reversal as an extremely sharp corner, and any outline grown from it extends the corner
// into a long spike. Tools that preprocess shapes (msdf-atlas-gen with Skia) removed them.
constexpr double SpurLengthTolerance = 0.005;

// Removes short line edges that reverse direction against an adjacent edge, joining the
// following edge to the removed edge's start.
void removeSpurs(msdfgen::Shape &shape) {
    for (msdfgen::Contour &contour : shape.contours) {
        bool removed = true;
        while (removed && contour.edges.size() > 3) {
            removed = false;
            const size_t count = contour.edges.size();
            for (size_t i = 0; i < count; ++i) {
                msdfgen::EdgeSegment *edge = contour.edges[i];
                if (edge->type() != msdfgen::LinearSegment::EDGE_TYPE)
                    continue;
                const msdfgen::Point2 start = edge->point(0), end = edge->point(1);
                const msdfgen::Vector2 along = end - start;
                if (along.length() >= SpurLengthTolerance)
                    continue;
                msdfgen::EdgeSegment *previous = contour.edges[(i + count - 1) % count];
                msdfgen::EdgeSegment *next = contour.edges[(i + 1) % count];
                const bool reverses = along.length() == 0.0
                    || msdfgen::dotProduct(previous->direction(1).normalize(), along.normalize()) < 0.0
                    || msdfgen::dotProduct(along.normalize(), next->direction(0).normalize()) < 0.0;
                if (!reverses)
                    continue;
                next->moveStartPoint(start);
                contour.edges.erase(contour.edges.begin() + static_cast<std::ptrdiff_t>(i));
                removed = true;
                break;
            }
        }
    }
}

// Glyphs built from overlapping contours (common in fonts exported from variable
// masters) produce distance field artifacts where a contour's hidden edges run inside
// another contour. Such glyphs are replaced by the union of their flattened contours;
// glyphs without overlaps keep their exact curves.
void resolveOverlappingContours(msdfgen::Shape &shape) {
    if (shape.contours.empty())
        return;
    Clipper2Lib::Paths64 paths;
    paths.reserve(shape.contours.size());
    double inputArea = 0.0;
    for (const msdfgen::Contour &contour : shape.contours) {
        Clipper2Lib::Path64 path;
        for (const msdfgen::EdgeHolder &edge : contour.edges)
            flattenEdge(*edge, path);
        if (path.size() < 3)
            continue;
        inputArea += Clipper2Lib::Area(path);
        paths.push_back(std::move(path));
    }
    if (paths.empty())
        return;

    Clipper2Lib::Paths64 merged = Clipper2Lib::Union(paths, Clipper2Lib::FillRule::NonZero);
    double mergedArea = 0.0;
    for (const Clipper2Lib::Path64 &path : merged)
        mergedArea += Clipper2Lib::Area(path);
    // Disjoint contours and properly nested holes leave the total area unchanged, since
    // the union of non-overlapping integer paths introduces no new vertices.
    if (std::abs(std::abs(mergedArea) - std::abs(inputArea)) <= 1.0)
        return;

    // Keep the font's original winding direction.
    const bool reverse = (mergedArea < 0.0) != (inputArea < 0.0);
    shape.contours.clear();
    for (Clipper2Lib::Path64 &path : merged) {
        if (path.size() < 3)
            continue;
        if (reverse)
            std::reverse(path.begin(), path.end());
        msdfgen::Contour &contour = shape.addContour();
        for (size_t i = 0; i < path.size(); ++i) {
            const Clipper2Lib::Point64 &a = path[i];
            const Clipper2Lib::Point64 &b = path[(i + 1) % path.size()];
            contour.addEdge(msdfgen::EdgeHolder(
                msdfgen::Point2(a.x / ClipperScale, a.y / ClipperScale),
                msdfgen::Point2(b.x / ClipperScale, b.y / ClipperScale)));
        }
    }
    shape.normalize();
}

// Computes the pixel-aligned glyph box
void layoutDistanceFieldGlyph(Glyph &glyph, const Options &options) {
    if (glyph.shape.contours.empty())
        return;
    msdfgen::Shape::Bounds bounds = glyph.shape.getBounds();
    if (!(bounds.l < bounds.r && bounds.b < bounds.t))
        return;

    const double scale = options.size;
    const double outward = -options.rangeLower + OuterPixelPadding;
    const int x0 = static_cast<int>(std::floor(bounds.l * scale - outward));
    const int y0 = static_cast<int>(std::floor(bounds.b * scale - outward));
    const int x1 = static_cast<int>(std::ceil(bounds.r * scale + outward));
    const int y1 = static_cast<int>(std::ceil(bounds.t * scale + outward));

    glyph.originX = x0;
    glyph.originY = y0;
    glyph.width = static_cast<uint32_t>(x1 - x0);
    glyph.height = static_cast<uint32_t>(y1 - y0);
    glyph.left = x0 / scale;
    glyph.bottom = y0 / scale;
    glyph.right = x1 / scale;
    glyph.top = y1 / scale;
}

void renderBitmapGlyph(FT_Face face, Glyph &glyph, const Options &options) {
    if (FT_Load_Glyph(face, glyph.glyphIndex, FT_LOAD_RENDER | FT_LOAD_TARGET_NORMAL))
        fail("failed to render glyph " + std::to_string(glyph.glyphIndex));
    FT_GlyphSlot slot = face->glyph;
    const double scale = options.size;
    glyph.advance = slot->advance.x / 64.0 / scale;

    const FT_Bitmap &bitmap = slot->bitmap;
    if (bitmap.width == 0 || bitmap.rows == 0)
        return;
    if (bitmap.pixel_mode != FT_PIXEL_MODE_GRAY && bitmap.pixel_mode != FT_PIXEL_MODE_MONO)
        fail("unsupported bitmap pixel mode for glyph " + std::to_string(glyph.glyphIndex));

    glyph.width = bitmap.width;
    glyph.height = bitmap.rows;
    glyph.channels = 1;
    glyph.left = slot->bitmap_left / scale;
    glyph.top = slot->bitmap_top / scale;
    glyph.right = (slot->bitmap_left + static_cast<int>(bitmap.width)) / scale;
    glyph.bottom = (slot->bitmap_top - static_cast<int>(bitmap.rows)) / scale;
    glyph.pixels.resize(static_cast<size_t>(bitmap.width) * bitmap.rows);
    for (unsigned y = 0; y < bitmap.rows; ++y) {
        const unsigned char *row = bitmap.buffer + static_cast<ptrdiff_t>(y) * bitmap.pitch;
        for (unsigned x = 0; x < bitmap.width; ++x) {
            uint8_t value = bitmap.pixel_mode == FT_PIXEL_MODE_MONO
                ? ((row[x >> 3] >> (7 - (x & 7))) & 1 ? 255 : 0)
                : row[x];
            glyph.pixels[static_cast<size_t>(y) * bitmap.width + x] = value;
        }
    }
}

void writeU32(FILE *file, uint32_t value) {
    uint8_t bytes[4] = {
        static_cast<uint8_t>(value), static_cast<uint8_t>(value >> 8),
        static_cast<uint8_t>(value >> 16), static_cast<uint8_t>(value >> 24) };
    std::fwrite(bytes, 1, 4, file);
}

void writeF64(FILE *file, double value) {
    uint64_t bits;
    std::memcpy(&bits, &value, sizeof bits);
    writeU32(file, static_cast<uint32_t>(bits));
    writeU32(file, static_cast<uint32_t>(bits >> 32));
}

} // namespace

int main(int argc, char **argv) {
    const Options options = parseArguments(getArguments(argc, argv));
    const std::vector<uint32_t> glyphIndices = readGlyphIndices(options.glyphsPath);
    const std::vector<uint8_t> fontData = readFile(options.fontPath);

    FT_Library library;
    if (FT_Init_FreeType(&library))
        fail("failed to initialize FreeType");
    FT_Face face;
    if (FT_New_Memory_Face(library, fontData.data(), static_cast<FT_Long>(fontData.size()), 0, &face))
        fail("failed to load font: " + options.fontPath);
    if (!FT_IS_SCALABLE(face))
        fail("font is not scalable: " + options.fontPath);

    msdfgen::FontHandle *font = msdfgen::adoptFreetypeFont(face);

    if (options.format == GlyphFormat::Bitmap) {
        FT_F26Dot6 charSize = static_cast<FT_F26Dot6>(std::lround(options.size * 64.0));
        if (FT_Set_Char_Size(face, 0, charSize, 72, 72))
            fail("failed to set bitmap size");
    }

    std::vector<Glyph> glyphs;
    glyphs.reserve(glyphIndices.size());
    for (uint32_t glyphIndex : glyphIndices) {
        if (glyphIndex >= static_cast<uint32_t>(face->num_glyphs))
            fail("glyph " + std::to_string(glyphIndex) + " is out of range: the font has " + std::to_string(face->num_glyphs) + " glyphs");
        Glyph glyph;
        glyph.glyphIndex = glyphIndex;
        if (options.format == GlyphFormat::Bitmap) {
            renderBitmapGlyph(face, glyph, options);
        }
        else {
            if (!msdfgen::loadGlyph(glyph.shape, font, msdfgen::GlyphIndex(glyphIndex), msdfgen::FONT_SCALING_EM_NORMALIZED))
                fail("failed to load glyph " + std::to_string(glyphIndex));
            glyph.shape.normalize();
            removeSpurs(glyph.shape);
            resolveOverlappingContours(glyph.shape);
            layoutDistanceFieldGlyph(glyph, options);
            if (options.format != GlyphFormat::Sdf)
                msdfgen::edgeColoringInkTrap(glyph.shape, EdgeColoringAngleThreshold, 0);
        }
        glyphs.push_back(std::move(glyph));
    }

    if (options.format != GlyphFormat::Bitmap) {
        unsigned threadCount = options.threads ? options.threads : std::max(1u, std::thread::hardware_concurrency());
        threadCount = std::min<unsigned>(threadCount, static_cast<unsigned>(std::max<size_t>(glyphs.size(), 1)));
        std::atomic<size_t> next(0);
        auto worker = [&]() {
            for (size_t i; (i = next.fetch_add(1)) < glyphs.size();)
                generateDistanceField(glyphs[i], options);
        };
        std::vector<std::thread> threads;
        for (unsigned i = 1; i < threadCount; ++i)
            threads.emplace_back(worker);
        worker();
        for (std::thread &thread : threads)
            thread.join();
    }

    FILE *file = openFile(options.outputPath, "wb");
    if (!file)
        fail("cannot create output file: " + options.outputPath);
    std::fwrite("GLGN", 1, 4, file);
    writeU32(file, 2);
    writeU32(file, static_cast<uint32_t>(glyphs.size()));
    for (const Glyph &glyph : glyphs) {
        writeU32(file, glyph.glyphIndex);
        writeF64(file, glyph.advance);
        writeF64(file, glyph.left);
        writeF64(file, glyph.bottom);
        writeF64(file, glyph.right);
        writeF64(file, glyph.top);
        writeU32(file, glyph.width);
        writeU32(file, glyph.height);
        writeU32(file, glyph.channels);
        if (!glyph.pixels.empty())
            std::fwrite(glyph.pixels.data(), 1, glyph.pixels.size(), file);
    }
    if (std::fclose(file) != 0)
        fail("failed to write output file: " + options.outputPath);

    msdfgen::destroyFont(font);
    FT_Done_Face(face);
    FT_Done_FreeType(library);
    return 0;
}
