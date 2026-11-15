# Sound banks and pools

## Sound banks

A [`SoundBank`](../api/SoundBank.md) builds many short sounds into one, so they load in one
request and decode once. The sounds of a folder make one bank, named `soundbank` unless `Name` says otherwise:

```xml
<SoundBankContent Include="Content\Sounds\*.wav">
  <Name>sounds</Name>
</SoundBankContent>
```

Each sound becomes a [`SoundSegment`](../api/SoundSegment.md) of the bank, named after its file:

```csharp
protected override async Task OnStart()
{
    this.InitializeToolkit();
    var bank = await this.Content.LoadAsync<SoundBank>("Sounds/sounds");

    this.coins = new SoundPool(this.Audio, bank.GetSegment("coin"), voiceLimit: 3);
}
```

In Visual Studio, the [DotGame Toolkit extension](../../vs/README.md) shows a bank's settings in its
folder's Properties window, and writes them to the item that includes the folder's sounds.

## Sound pools

A [`SoundPool`](../api/SoundPool.md) plays overlapping copies of a sound. It owns its voices, and
limits how many play at once, so a hundred coins in a second don't make a hundred sounds. When all of its
voices are busy, `Play` restarts the oldest:

```csharp
private void CollectCoin()
{
    this.coins.Play();
}

private void CollectQuietly()
{
    this.coins.Play(volume: 0.5f);
}
```

A pool also takes a plain [`Sound`](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/api/Framework/Sound.md), and it's yours to dispose. `StopAll`
stops every copy, and its `volumeControl` argument is the [`VolumeControl`](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/audio.md#volume-and-fades) they
play through.

To fade out everything a pool is playing, without cutting into what it plays next:

```csharp
private void FadeOutCoins()
{
    this.coins.FadeOutAll(TimeSpan.FromSeconds(0.5), this.Time);
}
```

## Parts of a sound

A segment can be part of a sound, and a single voice can play one, through the toolkit's `CreateVoice`
extension. This plays only the chime's first note:

```csharp
private SoundVoice MakeFirstNoteVoice(SoundBank bank)
{
    var chime = bank.GetSegment("chime");
    var firstNote = chime.Sound.AsSegment(chime.Offset, TimeSpan.FromSeconds(0.15));
    return this.Audio.CreateVoice(firstNote);
}
```

## See also

- [Audio](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/audio.md), for voices, music and volume
- [`SoundBank`](../api/SoundBank.md), [`SoundPool`](../api/SoundPool.md)
- The toolkit sample's Sound page
