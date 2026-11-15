// The names the guides' samples share, for tools/api-docs/check-docs.cs: a game's fields and
// helpers that the guides' fragments use. Not part of any project: check-docs.cs compiles it with
// the samples.

internal abstract class Prelude : Game
{
    protected Texture texture = null!, sheet = null!;
    protected RenderTexture canvas = null!;
    protected Shader wave = null!, textShader = null!;
    protected ShaderParameter time = null!;
    protected SpriteBatch spriteBatch = null!;
    protected Vector2 position, previousPosition, velocity, start, end, playerPosition;
    protected RectangleInt pageBounds, button;
    protected int score, levelNumber;
    protected object? solution;
    protected float zoom, menuPulse, elapsed;
    protected Music music = null!;
    protected VolumeControl musicVolume = null!, effectsVolume = null!;
    protected SoundVoice jumpVoice = null!;
    protected Sound jump = null!;
    protected readonly SoundVoice[] coinVoices = new SoundVoice[3];
    protected int nextCoinVoice;
    protected TextInput nameInput = null!;
    protected string playerName = string.Empty;
    protected DelayHandle? timer;
    protected SpriteFont font = null!;
    protected TextLayout layout = null!;
    protected ViewportScaler scaler = null!;
    protected Camera camera = null!;
    protected Vector2 _position;
    protected TextureAtlas atlas = null!;
    protected TextureRegion heart = null!;
    protected NinePatch panel = null!;
    protected SpriteAnimation idle = null!, run = null!, swing = null!;
    protected SpriteAnimationPlayer hero = null!;
    protected bool isMoving, facingLeft;
    protected ParticleSystem particles = null!;
    protected ParticleEmitter trail;
    protected SoundPool coins = null!;
    protected object ComputeSolution() => new();
    protected void Jump() { }
    protected void ShowMenu(Vector2 at) { }
    protected void ShowPauseMenu() { }
    protected void Parse(string name) { }
    protected void HitEnemies() { }
}

internal sealed class MainGame : Game { }
