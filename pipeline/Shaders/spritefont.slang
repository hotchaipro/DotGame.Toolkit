// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

// The text shader for DotGame.Toolkit SpriteFont, for every glyph format and every text
// effect. Injected into projects that contain SpriteFontContent items under the content key
// _toolkit/Shaders/spritefont, and loaded with each font by SpriteFontLoader.
//
// The shader is stateless: it has no parameters. Everything it needs besides the texture travels
// with each quad in the sprite batch's per-sprite shader data (TEXCOORD1), which
// SpriteFont.DrawString writes:
//
//   X  Distance offset, in atlas texels: the font's distance-range middle plus the layer's
//      emboldening (the face's, or the outline width, glow width or shadow spread). Positive
//      values move the edges outward. SpriteFont.DrawString keeps it, plus half the softness,
//      inside the encoded range.
//   Y  Softness, in atlas texels: how much wider than one screen pixel the edge's fade is.
//   Z  Miter limit, as SVG's stroke-miterlimit: how many times the emboldening a corner may
//      extend before it's cut back. 1 draws round corners; a large value is unlimited.
//   W  The font: its distance range in atlas texels, positive to read the median of the three
//      channels (Msdf and Sdf), negative to read the true distance in alpha as well (Mtsdf),
//      and zero for a bitmap font or any other sprite, which draws as with the default sprite
//      shader. So sprites drawn without shader data can share a batch with text.
//
// The atlas is rendered at the font's em size, so an atlas texel is a pixel of the font's
// native size. The atlas page's size comes from the texture.
//
// Mtsdf pages store the three distance channels in RGB and the true distance in alpha; Sdf pages
// replicate the single true distance to RGB, so the median of RGB is the distance for every
// format. Msdf has no true distance (its alpha is opaque), so it draws sharp, unlimited corners
// whatever the miter limit.
//
// The median keeps corners sharp, so moving the edges outward extends each corner along its
// edges like a miter: a corner of angle θ reaches e / sin(θ/2) from the glyph's corner for an
// emboldening e. Limiting the median to the true distance plus an allowance a cuts the corner
// at e + a from the glyph's corner, so a = (limit - 1) * e cuts it at limit * e, as SVG does.
// The true distance is only encoded within half the distance range of the glyph, so a cut can't
// be further out than that: with too small a range, corners are cut there, short of the limit.
// A small fixed allowance, fading out as the edges move outward, is kept as well: bilinear
// sampling between texels whose channels disagree can put the median inside the glyph where it
// isn't, drawing thin stray lines (seams) beside a stem, while the median keeps the face's own
// corners sharp where it differs from the true distance by less.
//
// Pages are linear, straight alpha, no mipmaps; sample bilinear, clamp. The output is
// premultiplied, like the sprite batch's default shader.

Sampler2D SpriteTextureSampler;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
    float4 CustomData : TEXCOORD1;
};

float Median(float a, float b, float c)
{
    return max(min(a, b), min(max(a, b), c));
}

[shader("pixel")]
float4 psMain(VertexShaderOutput input) : SV_Target
{
    float2 p = input.TextureCoordinate;
    float distanceOffset = input.CustomData.x;
    float softness = input.CustomData.y;
    float miterLimit = input.CustomData.z;
    float signedRange = input.CustomData.w;

    float4 sample = SpriteTextureSampler.Sample(p);

    // The texture coordinate's screen derivatives, taken first, and the shader returns once at
    // its end: an early return makes the compiler wrap the body in a loop to break out of, and
    // WebGL returned zero derivatives inside it, which drew distance-field text without
    // anti-aliasing
    float width;
    float height;
    SpriteTextureSampler.GetDimensions(width, height);
    float2 atlasSize = float2(width, height);
    float2 dx = ddx(p) * atlasSize;
    float2 dy = ddy(p) * atlasSize;

    // A bitmap font, whose premultiplied white glyphs make this the color times the coverage,
    // or any other sprite, drawn without shader data: drawn as the default sprite shader would,
    // so text and other sprites can share a batch
    float4 result = sample * input.Color;
    if (signedRange != 0.0f)
    {
        result = DrawDistanceField(sample, input.Color, dx, dy, distanceOffset, softness, miterLimit, signedRange);
    }

    return result;
}

// The color of distance-field text: the glyph's coverage of the pixel, from its distance to the
// edge in atlas texels (dx and dy are the texture coordinate's derivatives in texels)
float4 DrawDistanceField(
    float4 sample,
    float4 color,
    float2 dx,
    float2 dy,
    float distanceOffset,
    float softness,
    float miterLimit,
    float signedRange)
{
    float distanceRange = abs(signedRange);

    // Atlas texels per screen pixel. Averaging the squared derivatives keeps the value the same
    // when the text is rotated, unlike the sum of fwidth. It is capped so that heavily minified
    // text stays legible, and kept away from zero.
    float texelsPerPixel = clamp(sqrt(0.5f * (dot(dx, dx) + dot(dy, dy))), 1.0e-5f, 0.25f * distanceRange);

    // Signed distance to the edge in atlas texels, positive inside the glyph
    float distance = (Median(sample.r, sample.g, sample.b) - 0.5f) * distanceRange;

    if (signedRange < 0.0f)
    {
        float trueDistance = (sample.a - 0.5f) * distanceRange;
        float emboldening = max(distanceOffset, 0.0f);
        float allowance = max(2.0f - emboldening, 0.0f) + (max(miterLimit, 1.0f) - 1.0f) * emboldening;

        // The true distance saturates half the range from the glyph, so a cut further out than
        // that would never happen: cut there instead, unless the miter is unlimited
        if (miterLimit < 1000.0f)
        {
            allowance = min(allowance, max(0.5f * distanceRange - 1.0f - emboldening, 0.0f));
        }

        distance = min(distance, trueDistance + allowance);
    }

    float signedDistance = distance + distanceOffset;

    // Fade the edge over one screen pixel, plus the softness
    float opacity = saturate(signedDistance / (texelsPerPixel + softness) + 0.5f);

    return color * opacity;
}
