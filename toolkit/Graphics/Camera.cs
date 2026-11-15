// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using DotGame.Framework;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// A view of a 2D world: where it looks, how close, and at what angle. Begin a sprite batch with its
/// <see cref="Transform"/> to draw the world through it.
/// </summary>
/// <example>
/// <code>
/// camera.Position = player.Position;
/// using var batch = spriteBatch.Begin(camera.Transform);
/// </code>
/// </example>
public sealed class Camera
{
    private Vector2 _position;
    private float _zoom = 1f;
    private Angle _rotation;
    private Vector2 _origin;
    private Matrix3x2 _transform = Matrix3x2.Identity;
    private bool _isTransformCurrent = true;

    /// <summary>
    /// Gets or sets the point in the world the camera looks at.
    /// </summary>
    public Vector2 Position
    {
        get => this._position;
        set
        {
            this._position = value;
            this._isTransformCurrent = false;
        }
    }

    /// <summary>
    /// Gets or sets how close the camera is: above 1 magnifies the world, and below 1 shows more of it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't positive.</exception>
    public float Zoom
    {
        get => this._zoom;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            this._zoom = value;
            this._isTransformCurrent = false;
        }
    }

    /// <summary>
    /// Gets or sets the camera's angle. The world appears to turn the other way.
    /// </summary>
    public Angle Rotation
    {
        get => this._rotation;
        set
        {
            this._rotation = value;
            this._isTransformCurrent = false;
        }
    }

    /// <summary>
    /// Gets or sets where on the screen <see cref="Position"/> appears: the top-left corner by
    /// default, and the middle of the view for a camera that follows something.
    /// </summary>
    public Vector2 Origin
    {
        get => this._origin;
        set
        {
            this._origin = value;
            this._isTransformCurrent = false;
        }
    }

    /// <summary>
    /// Gets the transform from the world to the screen, for <c>SpriteBatch.Begin</c>. To use it with
    /// a <see cref="ViewportScaler"/>, multiply it by the scaler's: <c>camera.Transform * scaler.Transform</c>.
    /// </summary>
    public Matrix3x2 Transform
    {
        get
        {
            if (!this._isTransformCurrent)
            {
                this._transform = Matrix3x2.CreateTranslation(-this._position)
                    * Matrix3x2.CreateRotation(-this._rotation.Radians)
                    * Matrix3x2.CreateScale(this._zoom)
                    * Matrix3x2.CreateTranslation(this._origin);
                this._isTransformCurrent = true;
            }

            return this._transform;
        }
    }

    /// <summary>
    /// Gets where a point in the world appears on the screen.
    /// </summary>
    public Vector2 WorldToScreen(
        Vector2 world)
    {
        return Vector2.Transform(world, this.Transform);
    }

    /// <summary>
    /// Gets the point in the world at a point on the screen, such as a pointer's position.
    /// </summary>
    public Vector2 ScreenToWorld(
        Vector2 screen)
    {
        // NOTE: A positive zoom and a rotation always have an inverse
        Matrix3x2.Invert(this.Transform, out var inverse);
        return Vector2.Transform(screen, inverse);
    }
}
