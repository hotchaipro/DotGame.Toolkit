# Cameras, scaling and shapes

## Designing for one size

[`ViewportScaler`](../api/ViewportScaler.md) lets you draw in the size you designed for, whatever
the window or device. [`Camera`](../api/Camera.md) moves, zooms and turns the view of a world.
Their transforms multiply:

```csharp
protected override void OnDraw()
{
    this.scaler.Update(this.ClientBounds);
    this.camera.Position = this.playerPosition;

    this.Graphics.Clear(Color.Black);

    var spriteBatch = this.Graphics.SharedSpriteBatch;
    using var scissor = this.Graphics.UseScissor(this.scaler.Viewport);   // Keeps Fit's bars clear
    using var batch = spriteBatch.Begin(this.camera.Transform * this.scaler.Transform);
    spriteBatch.Draw(this.texture, this.playerPosition, Color.White);
}
```

Make them once, with the size you designed for. The camera's origin is the point of the screen it looks
through, usually the design's center:

```csharp
private readonly ViewportScaler scaler = new(new Vector2Int(400, 300), ViewportScaleMode.Fit);
private readonly Camera camera = new() { Origin = new Vector2(200, 150) };
```

| [`ViewportScaleMode`](../api/ViewportScaleMode.md) | The design |
|---|---|
| `Fit` | All shows, as large as fits, with bars on the sides left over |
| `Fill` | Fills the screen, cut off on the sides that don't fit |
| `Stretch` | Fills the screen exactly, stretched to its shape |
| `Expand` | All shows, as large as fits, and the sides left over show more of the world |
| `ExpandVertical` | As `Expand` on a taller screen, showing more below; as `Fit` on a wider one |
| `ExpandHorizontal` | As `Expand` on a wider screen, showing more to the right; as `Fit` on a taller one |
| `IntegerFit` | As `Fit`, at a whole number of pixels per design pixel, for pixel art, with `Sampler.PointClamp` |

## From the screen to the world

A pointer is in screen coordinates, so go back through the scaler, and then the camera:

```csharp
protected override void OnPointerEvent(PointerEvent e)
{
    var world = this.camera.ScreenToWorld(this.scaler.ScreenToDesign(e.Position));
    if (e.Kind == PointerEventKind.Began)
    {
        this.playerPosition = world;
    }
}
```

`WorldToScreen` and `DesignToScreen` go the other way, for putting something over a point in the world.

Remember [the safe area](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/game-loop.md#the-window-changing). A scaled design can reach under a notch, so
keep controls inside `SafeAreaBounds`.

## Shapes

[`SpriteBatchShapeExtensions`](../api/SpriteBatchShapeExtensions.md) adds lines, rectangles, circles and polygons to
a sprite batch. They draw in order with your sprites, and through the batch's transform:

```csharp
private void DrawShapes(SpriteBatch spriteBatch)
{
    spriteBatch.DrawLine(new Vector2(10, 10), new Vector2(200, 80), Color.White, thickness: 2);
    spriteBatch.DrawRectangle(new Rectangle(10, 100, 120, 60), Color.Red, thickness: 3);
    spriteBatch.FillRectangle(new Rectangle(150, 100, 120, 60), Color.Blue);
    spriteBatch.DrawCircle(new Vector2(60, 240), 40, Color.Green, thickness: 2);
    spriteBatch.FillCircle(new Vector2(180, 240), 40, Color.FromRgb(0xFFA500));
    spriteBatch.FillTriangle(new Vector2(260, 200), new Vector2(340, 200), new Vector2(300, 280), Color.Yellow);
}
```

`DrawPolygon` and `FillPolygon` take a list of points, and `FillPolygon` handles a convex shape. Circles
choose how many segments to use from their radius unless you say.

A rectangle or a line costs what a sprite does, and a circle about one sprite for each side: 20 at a radius
of 40, and twice that for an outline. They're fine by the dozen, but for hundreds of circles, draw a circle
sprite. Shapes use a texture of their own, so drawing them between other sprites ends the batch's run each time:
draw them together where you can. The [stats overlay](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/diagnostics.md)'s detailed mode shows the sprites and draw
calls a frame takes.

Edges aren't antialiased except by [multisampling](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/graphics.md#smooth-edges), so shapes that need to look
soft at any size are better as art.

For triangles with a color at each corner, `FillTriangles` is
[`DrawTriangles`](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/graphics.md#triangles) without a texture.

## See also

- [`Camera`](../api/Camera.md), [`ViewportScaler`](../api/ViewportScaler.md),
  [`SpriteBatchShapeExtensions`](../api/SpriteBatchShapeExtensions.md)
- The toolkit sample's Camera and Shapes pages
