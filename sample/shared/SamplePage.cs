// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Framework.Input;

namespace Sample;

/// <summary>
/// One page of the sample: a self-contained example of one part of the toolkit.
/// </summary>
internal abstract class SamplePage : IDisposable
{
    protected SamplePage(
        ToolkitSample game)
    {
        this.Game = game;
    }

    protected ToolkitSample Game { get; }

    public abstract string Title { get; }

    public abstract string Hint { get; }

    /// <summary>
    /// Loads the page's content, once, when the game starts.
    /// </summary>
    public virtual Task Load() => Task.CompletedTask;

    public virtual void Enter()
    {
    }

    public virtual void Leave()
    {
    }

    public virtual void Update(
        TimeSpan elapsed)
    {
    }

    /// <summary>
    /// Draws the page inside <paramref name="bounds"/>. The sprite batch isn't begun.
    /// </summary>
    public abstract void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds);

    public virtual void OnPointerEvent(
        PointerEvent e)
    {
    }

    public virtual void OnKeyEvent(
        KeyEvent e)
    {
    }

    /// <summary>
    /// Releases what the page created, such as sound pools, when the game stops.
    /// </summary>
    public virtual void Dispose()
    {
    }

    protected static bool IsTap(
        in PointerEvent e,
        RectangleInt bounds)
    {
        return (e.Kind == PointerEventKind.Began) && bounds.Contains(e.Position);
    }
}
