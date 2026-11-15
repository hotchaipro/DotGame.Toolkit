// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Where a line of text may break. It's a practical part of Unicode's line breaking: spaces, hyphens,
/// between CJK characters, and the punctuation rules of Chinese and Japanese (kinsoku). It needs no
/// dictionary, so Thai, Lao and Khmer break only at spaces.
/// </summary>
internal static class LineBreaking
{
    private const int ZeroWidthSpace = 0x200B;
    private const int IdeographicSpace = 0x3000;

    // Closing punctuation, and the small kana and marks that belong to what's before them
    private const string NoStart = "!),.:;?]}%"
        + "\u00BB\u2019\u201D\u2026\u2025\u30FB"
        + "\u3001\u3002\u3005\u3009\u300B\u300D\u300F\u3011\u3015\u3017\u3019\u301F\u303B"
        + "\u3041\u3043\u3045\u3047\u3049\u3063\u3083\u3085\u3087\u308E\u3095\u3096"
        + "\u309D\u309E"
        + "\u30A1\u30A3\u30A5\u30A7\u30A9\u30C3\u30E3\u30E5\u30E7\u30EE\u30F5\u30F6"
        + "\u30FC\u30FD\u30FE"
        + "\uFF01\uFF09\uFF0C\uFF0E\uFF1A\uFF1B\uFF1F\uFF3D\uFF5D\uFF60\uFF61\uFF63\uFF64";

    // Opening punctuation, which belongs to what's after it
    private const string NoEnd = "([{$"
        + "\u00A3\u00A5\u00AB\u2018\u201C"
        + "\u3008\u300A\u300C\u300E\u3010\u3014\u3016\u3018\u301D"
        + "\uFF04\uFF08\uFF3B\uFF5B\uFF5F\uFF62\uFFE5";

    /// <summary>Whether it's a space a line can end at, and which the line then doesn't show.</summary>
    public static bool IsBreakSpace(
        int codePoint)
    {
        return (codePoint == ' ') || (codePoint == IdeographicSpace);
    }

    /// <summary>Whether it's not to start a line.</summary>
    public static bool IsNoStart(
        int codePoint)
    {
        return (codePoint <= char.MaxValue) && NoStart.Contains((char)codePoint);
    }

    /// <summary>Whether a line may end between two neighboring characters.</summary>
    public static bool CanBreakBetween(
        int before,
        int after)
    {
        if (IsBreakSpace(after) || (before == ZeroWidthSpace))
        {
            return true;
        }

        if ((before == '-') && (after <= char.MaxValue) && char.IsLetter((char)after))
        {
            return true;
        }

        if (IsNoStart(after) || ((before <= char.MaxValue) && NoEnd.Contains((char)before)))
        {
            return false;
        }

        return IsBreakableAnywhere(before) || IsBreakableAnywhere(after);
    }

    // Han, kana, and CJK punctuation and fullwidth forms. Not Hangul: Korean breaks at its spaces, and
    // within a word only when it doesn't fit on a line, as a Latin one does
    private static bool IsBreakableAnywhere(
        int codePoint)
    {
        return ((codePoint >= 0x2E80) && (codePoint <= 0x312F))
            || ((codePoint >= 0x3400) && (codePoint <= 0x4DBF))
            || ((codePoint >= 0x4E00) && (codePoint <= 0x9FFF))
            || ((codePoint >= 0xF900) && (codePoint <= 0xFAFF))
            || ((codePoint >= 0xFF00) && (codePoint <= 0xFFEF))
            || ((codePoint >= 0x20000) && (codePoint <= 0x3FFFF));
    }
}
