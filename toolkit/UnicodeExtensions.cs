// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit;

internal static class UnicodeExtensions
{
    public static CodePointEnumerator EnumerateCodePoints(
        this ReadOnlySpan<char> text)
    {
        return new CodePointEnumerator(text);
    }

    public static int Utf16SequenceLength(
        this int codePoint)
    {
        return (codePoint > 0xffff) ? 2 : 1;
    }

    public ref struct CodePointEnumerator
    {
        private readonly ReadOnlySpan<char> _text;
        private int _index;

        public CodePointEnumerator(
            ReadOnlySpan<char> text)
        {
            this._text = text;
            this._index = 0;
            this.Current = 0;
        }

        public int Current
        {
            get;
            private set;
        }

        public readonly CodePointEnumerator GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            int index = this._index;
            if ((uint)index >= (uint)this._text.Length)
            {
                return false;
            }

            char c = this._text[index];

            // Fast path: a non-surrogate char is its own code point.
            if (!Char.IsHighSurrogate(c))
            {
                this.Current = c;
                this._index = index + 1;
                return true;
            }

            // High surrogate followed by a valid low surrogate: combine
            // into a supplementary-plane code point.
            int next = index + 1;
            if ((uint)next < (uint)this._text.Length
                && Char.IsLowSurrogate(this._text[next]))
            {
                this.Current = Char.ConvertToUtf32(c, this._text[next]);
                this._index = index + 2;
                return true;
            }

            // Unpaired high surrogate: emit it as-is and advance one char.
            this.Current = c;
            this._index = index + 1;
            return true;
        }
    }
}
