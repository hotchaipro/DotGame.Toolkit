// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Framework.Input;
using DotGame.Toolkit.Animation;
using DotGame.Toolkit.Audio;
using DotGame.Toolkit.Graphics;

namespace Sample.Pages;

/// <summary>
/// Sprite animation from atlas frames. A <see cref="SpriteAnimation"/> is shared and holds the
/// frames and their timing; a <see cref="SpriteAnimationPlayer"/> plays one for one thing on
/// screen.
/// </summary>
/// <remarks>
/// <para>
/// Frames are ordinary images in the atlas, named <c>animation_number</c>:
/// <c>character_idle_0.png</c> to <c>character_idle_11.png</c> make the animation <c>character_idle</c>.
/// </para>
/// <para>
/// <see cref="SpriteAnimationPlayer.Play"/> returns a handle for that play: <c>When</c> runs an
/// action on a frame (a sound on landing), and <c>Then</c> runs one when a <c>Once</c> animation
/// finishes (going back to idle).
/// </para>
/// </remarks>
internal sealed class AnimationPage : SamplePage
{
    private readonly SpriteAnimationPlayer _character = new();
    private SpriteAnimation _idle = null!;
    private SpriteAnimation _jump = null!;
    private SpriteAnimation _coin = null!;
    private SoundPool _jumpSound = null!;
    private SoundPool _landSound = null!;
    private bool _facingLeft;
    private int _jumps;

    private RectangleInt _jumpButton;
    private RectangleInt _speedButton;
    private RectangleInt _pauseButton;
    private RectangleInt _turnButton;

    public AnimationPage(
        ToolkitSample game)
        : base(game)
    {
    }

    public override string Title => "Sprite animation";

    public override string Hint => "Tap Jump or press Space. The coins share one animation and no player.";

    public override Task Load()
    {
        var game = this.Game;
        var atlas = game.Atlas;

        // Looping and once-only animations, straight from the atlas
        this._idle = atlas.CreateAnimation("character_idle", framesPerSecond: 12);
        this._jump = atlas.CreateAnimation("character_jump", framesPerSecond: 14, PlaybackMode.Once);

        // An animation built from frames with holds: the coin's frames are half a turn, which
        // loops, and it rests face-on for eight frames' worth of time before each turn
        var frames = new List<SpriteAnimationFrame>();
        var coinFrames = atlas.GetAnimationFrames("coin");
        for (int i = 0; i < coinFrames.Count; i++)
        {
            frames.Add(new SpriteAnimationFrame(coinFrames[i], (i == 0) ? 8 : 1));
        }

        this._coin = new SpriteAnimation(frames, framesPerSecond: 24, PlaybackMode.Loop);

        this._jumpSound = new SoundPool(game.Audio, game.Sounds.GetSegment("jump"), voiceLimit: 2);
        this._landSound = new SoundPool(game.Audio, game.Sounds.GetSegment("pop"), voiceLimit: 2);

        this._character.Play(this._idle);
        return Task.CompletedTask;
    }

    private void Jump()
    {
        // Jumping again before landing restarts the jump
        this._jumps++;
        this._jumpSound.Play();
        this._character.Play(this._jump, restart: true)
            .When(this._jump.Frames.Count - 1, () => this._landSound.Play())
            .Then(() => this._character.Play(this._idle));
    }

    public override void Update(
        TimeSpan elapsed)
    {
        // A player advances only when updated, at its own speed
        this._character.Update(elapsed);
    }

    public override void OnKeyEvent(
        KeyEvent e)
    {
        if ((e.Kind == KeyEventKind.Pressed) && (e.Key == Key.Space) && !e.IsRepeat)
        {
            this.Jump();
        }
    }

    public override void OnPointerEvent(
        PointerEvent e)
    {
        if (IsTap(e, this._jumpButton))
        {
            this.Jump();
        }
        else if (IsTap(e, this._speedButton))
        {
            this._character.Speed = (this._character.Speed >= 2) ? 0.5f : this._character.Speed * 2;
        }
        else if (IsTap(e, this._pauseButton))
        {
            if (this._character.IsPlaying)
            {
                this._character.Pause();
            }
            else
            {
                this._character.Resume();
            }
        }
        else if (IsTap(e, this._turnButton))
        {
            this._facingLeft = !this._facingLeft;
        }
    }

    public override void Dispose()
    {
        this._jumpSound.Dispose();
        this._landSound.Dispose();
        base.Dispose();
    }

    public override void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds)
    {
        var game = this.Game;
        using var batch = spriteBatch.Begin();

        // The buttons, left-aligned like the other pages: four 140 wide with 16 between them
        const int ButtonsWidth = (4 * 140) + (3 * 16);
        int x = bounds.X + 20;
        int y = bounds.Y + 422;

        // The character, centered over its buttons and drawn from its player: the current frame,
        // its feet on the ground (each frame's feet are 8 pixels above its bottom), flipped to face
        // left. A flip mirrors it in place, whatever the origin. The space above it is for its
        // jump, which rises 60 pixels.
        var ground = new Vector2(x + (ButtonsWidth / 2f), bounds.Y + 340);

        var origin = (this._character.Frame is { } characterFrame) ? new Vector2(characterFrame.Size.X * 0.5f, characterFrame.Size.Y - 8) : Vector2.Zero;
        var flip = this._facingLeft ? SpriteFlip.Horizontal : SpriteFlip.None;
        spriteBatch.Draw(this._character, ground, Color.White, origin: origin, flip: flip);

        var state = this._character.IsPlaying ? "playing" : "paused";
        var animation = (this._character.Animation == this._jump) ? "jump" : "idle";
        // The character's player, just above its buttons
        game.DrawText(spriteBatch, $"{animation}, frame {this._character.FrameIndex}, {state}, speed {this._character.Speed:0.##}x, jumps {this._jumps}", new Vector2(x, y - 34), 18, Color.FromRgb(0x3C465A));

        // Coins in lockstep: one shared animation, sampled at the game's time, with no player, so
        // the character's buttons don't affect them. Above the character, apart from its buttons.
        var time = game.Time.Total;
        for (int i = 0; i < 6; i++)
        {
            var frame = this._coin.GetFrame(time + TimeSpan.FromSeconds(i * 0.08));
            var position = new Vector2(ground.X + ((i - 2.5f) * 70), bounds.Y + 56);
            spriteBatch.Draw(frame, position, Color.White, Angle.Zero, (Vector2)frame.Size * 0.5f, Vector2.One);
        }

        this._jumpButton = new RectangleInt(x, y, 140, 56);
        this._speedButton = new RectangleInt(x + 156, y, 140, 56);
        this._pauseButton = new RectangleInt(x + 312, y, 140, 56);
        this._turnButton = new RectangleInt(x + 468, y, 140, 56);
        game.DrawButton(spriteBatch, this._jumpButton, "Jump");
        game.DrawButton(spriteBatch, this._speedButton, "Speed");
        game.DrawButton(spriteBatch, this._pauseButton, this._character.IsPlaying ? "Pause" : "Resume", !this._character.IsPlaying);
        game.DrawButton(spriteBatch, this._turnButton, "Turn", this._facingLeft);
    }
}
