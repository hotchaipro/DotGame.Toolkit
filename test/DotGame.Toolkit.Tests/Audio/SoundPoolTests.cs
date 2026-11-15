// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework.Audio;
using DotGame.Framework.Content;
using DotGame.Toolkit.Audio;
using Xunit;

namespace DotGame.Toolkit.Tests.Audio;

public sealed class SoundPoolTests
{
    private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(0.5);

    private static (FakeAudio Audio, SoundPool Pool) NewPool(
        int voiceLimit = SoundPool.DefaultVoiceLimit)
    {
        var audio = new FakeAudio();
        return (audio, new SoundPool(audio.Service, FakeAudio.CreateSound(), voiceLimit));
    }

    // Creating

    [Fact]
    public void Constructor_CreatesOneVoice_ForTheWholeSound()
    {
        var audio = new FakeAudio();

        var pool = new SoundPool(audio.Service, FakeAudio.CreateSound(seconds: 3));

        var voice = Assert.Single(audio.Voices);
        Assert.Equal(TimeSpan.Zero, voice.Offset);
        Assert.Equal(TimeSpan.FromSeconds(3), voice.Duration);
        Assert.Equal(SoundPool.DefaultVoiceLimit, pool.VoiceLimit);
    }

    [Fact]
    public void Constructor_WithASegment_BakesItIntoEachVoice()
    {
        var audio = new FakeAudio();
        var segment = FakeAudio.CreateSound().AsSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        var pool = new SoundPool(audio.Service, segment);
        pool.Play();
        pool.Play();

        Assert.Equal(2, audio.Voices.Count);
        Assert.All(audio.Voices, v => Assert.Equal(TimeSpan.FromSeconds(1), v.Offset));
        Assert.All(audio.Voices, v => Assert.Equal(TimeSpan.FromSeconds(2), v.Duration));
        Assert.Equal(TimeSpan.FromSeconds(2), pool.Segment.Duration);
    }

    [Fact]
    public void Constructor_RejectsNullArguments()
    {
        var audio = new FakeAudio();

        Assert.Throws<ArgumentNullException>(() => new SoundPool(null!, FakeAudio.CreateSound()));
        Assert.Throws<ArgumentNullException>(() => new SoundPool(audio.Service, (Sound)null!));
        Assert.Throws<ArgumentNullException>(() => new SoundPool(audio.Service, default(SoundSegment)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsALimitBelowOne(
        int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoundPool(new FakeAudio().Service, FakeAudio.CreateSound(), limit));
    }

    // Which voice plays

    [Fact]
    public void Play_ANonOverlappingCue_KeepsOneVoice()
    {
        var (audio, pool) = NewPool();

        for (int i = 0; i < 5; i++)
        {
            pool.Play();
            audio.Voices[0].Finish();
        }

        var voice = Assert.Single(audio.Voices);
        Assert.Equal(5, voice.Plays);
    }

    [Fact]
    public void Play_Overlapping_CreatesVoicesUpToTheLimit()
    {
        var (audio, pool) = NewPool(voiceLimit: 3);

        pool.Play();
        pool.Play();
        pool.Play();

        Assert.Equal(3, audio.Voices.Count);
        Assert.All(audio.Voices, v => Assert.True(v.IsPlaying));
    }

    [Fact]
    public void Play_PrefersAFinishedVoice_OverCreatingOne()
    {
        var (audio, pool) = NewPool();
        pool.Play();
        pool.Play();
        audio.Voices[0].Finish();

        pool.Play();

        Assert.Equal(2, audio.Voices.Count);
        Assert.Equal(2, audio.Voices[0].Plays);
    }

    [Fact]
    public void Play_AtTheLimit_StealsTheOldest_InTurn()
    {
        var (audio, pool) = NewPool(voiceLimit: 3);
        pool.Play();
        pool.Play();
        pool.Play();

        pool.Play();
        Assert.Equal([2, 1, 1], audio.Voices.Select(v => v.Plays));

        pool.Play();
        Assert.Equal([2, 2, 1], audio.Voices.Select(v => v.Plays));

        pool.Play();
        pool.Play();
        Assert.Equal([3, 2, 2], audio.Voices.Select(v => v.Plays));
        Assert.Equal(3, audio.Voices.Count);
    }

    [Fact]
    public void Play_AtTheLimit_StealsByWhenItStarted_NotBySlot()
    {
        var (audio, pool) = NewPool(voiceLimit: 3);
        pool.Play();
        pool.Play();
        pool.Play();

        // The first voice finishes and plays again, so the second is now the oldest
        audio.Voices[0].Finish();
        pool.Play();

        pool.Play();

        Assert.Equal([2, 2, 1], audio.Voices.Select(v => v.Plays));
    }

    [Fact]
    public void Prepare_CreatesVoicesUpToTheLimit_AndPlaysUseThem()
    {
        var (audio, pool) = NewPool(voiceLimit: 3);

        pool.Prepare(10);
        Assert.Equal(3, audio.Voices.Count);

        pool.Play();
        pool.Play();
        pool.Play();
        Assert.Equal(3, audio.Voices.Count);
    }

    [Fact]
    public void Play_ALimitOfOne_RestartsItsVoice()
    {
        var (audio, pool) = NewPool(voiceLimit: 1);

        pool.Play();
        pool.Play();

        var voice = Assert.Single(audio.Voices);
        Assert.Equal(2, voice.Plays);

        // Only the stolen voice needed stopping
        Assert.Equal(1, voice.Stops);
        Assert.True(voice.IsPlaying);
    }

    [Fact]
    public void Play_ReturnsTheVoiceItStarted()
    {
        var (audio, pool) = NewPool();

        var first = pool.PlayVoice();
        var second = pool.PlayVoice();

        Assert.NotSame(first, second);
        Assert.True(first.IsPlaying);
        Assert.True(second.IsPlaying);

        audio.Voices[0].Finish();
        Assert.False(first.IsPlaying);
        Assert.Same(first, pool.PlayVoice());
    }

    // Volume

    [Fact]
    public void Play_AtFullVolume_NeedsNoVolumeControl()
    {
        var (audio, pool) = NewPool();

        pool.Play();
        pool.Play(1f);

        Assert.Empty(audio.VolumeControls);
        Assert.All(audio.Voices, v => Assert.Null(v.VolumeControl));
    }

    [Fact]
    public void Play_AtLessThanFullVolume_GivesTheVoiceItsOwnControl()
    {
        var (audio, pool) = NewPool();

        var quiet = pool.PlayVoice(0.25f);
        var other = pool.PlayVoice(0.5f);

        Assert.Equal(2, audio.VolumeControls.Count);
        Assert.Equal(0.25f, quiet.VolumeControl!.Volume);
        Assert.Equal(0.5f, other.VolumeControl!.Volume);
        Assert.NotSame(quiet.VolumeControl, other.VolumeControl);
    }

    [Fact]
    public void Play_SetsTheVolumeBeforeThePlayStarts()
    {
        var (audio, pool) = NewPool();
        pool.Play(0.5f);
        audio.Log.Clear();
        audio.Voices[0].Finish();

        pool.Play(0.25f);

        // A finished voice needs no stop, which costs a platform call
        Assert.Equal(["volume", "play"], audio.Log);
    }

    [Fact]
    public void Play_AtFullVolume_RestoresAVoiceThatPlayedQuietly()
    {
        var (audio, pool) = NewPool();
        var voice = pool.PlayVoice(0.25f);
        audio.Voices[0].Finish();

        Assert.Same(voice, pool.PlayVoice());

        Assert.Equal(1f, voice.VolumeControl!.Volume);
        Assert.Single(audio.VolumeControls);
    }

    // Fading

    [Fact]
    public void Play_AVolumeThatIsNotANumber_IsRefused()
    {
        var (_, pool) = NewPool();

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Play(float.NaN));
    }

    [Fact]
    public void FadeOutAll_StopsTheVoices_WhenTheFadeHasEnded()
    {
        var (_, pool) = NewPool(8);
        var voice = pool.PlayVoice();
        var time = new TestClock();
        time.Update(TimeSpan.Zero);

        pool.FadeOutAll(TimeSpan.FromMilliseconds(100), time.Service);

        time.Update(TimeSpan.FromMilliseconds(50));
        Assert.True(voice.IsPlaying);

        time.Update(TimeSpan.FromMilliseconds(100));
        Assert.False(voice.IsPlaying);
    }

    [Fact]
    public void FadeOutAll_FadesOnlyThePlayingVoices()
    {
        var (audio, pool) = NewPool();
        pool.Play();
        pool.Play();
        audio.Voices[1].Finish();

        pool.FadeOutVoices(FadeTime);

        var control = Assert.Single(audio.VolumeControls);
        Assert.Equal(0f, control.FadeTarget);
        Assert.Equal(FadeTime, control.FadeDuration);
        Assert.NotNull(audio.Voices[0].VolumeControl);
        Assert.Null(audio.Voices[1].VolumeControl);
    }

    [Fact]
    public void FadeOutAll_KeepsTheVoicesPlaying_UntilStopFadedOut()
    {
        var (audio, pool) = NewPool();
        pool.Play();
        pool.Play();

        var fade = pool.FadeOutVoices(FadeTime);
        Assert.All(audio.Voices, v => Assert.True(v.IsPlaying));

        pool.StopFadedOut(fade);
        Assert.All(audio.Voices, v => Assert.False(v.IsPlaying));
    }

    [Fact]
    public void FadeOutAll_ReusesAVoicesControl()
    {
        var (audio, pool) = NewPool();
        pool.Play(0.5f);

        pool.FadeOutVoices(FadeTime);

        Assert.Equal(0f, Assert.Single(audio.VolumeControls).FadeTarget);
    }

    [Fact]
    public void Play_DuringAFade_LeavesTheNewPlayOutOfIt()
    {
        var (audio, pool) = NewPool(voiceLimit: 1);
        pool.Play();
        var fade = pool.FadeOutVoices(FadeTime);

        var voice = pool.PlayVoice();

        // The new play's volume cancels the fade
        Assert.Equal(1f, voice.VolumeControl!.Volume);
        Assert.Null(audio.VolumeControls[0].FadeTarget);

        pool.StopFadedOut(fade);
        Assert.True(voice.IsPlaying);
    }

    [Fact]
    public void FadeOutAll_Twice_OnlyTheLatestFadeStopsAVoice()
    {
        var (audio, pool) = NewPool();
        pool.Play();

        var first = pool.FadeOutVoices(FadeTime);
        var second = pool.FadeOutVoices(FadeTime);
        Assert.NotEqual(first, second);

        pool.StopFadedOut(first);
        Assert.True(audio.Voices[0].IsPlaying);

        pool.StopFadedOut(second);
        Assert.False(audio.Voices[0].IsPlaying);
    }

    [Fact]
    public void StopFadedOut_LeavesVoicesNotInThatFade()
    {
        var (audio, pool) = NewPool();
        pool.Play();
        var fade = pool.FadeOutVoices(FadeTime);
        pool.Play();

        pool.StopFadedOut(fade);

        Assert.False(audio.Voices[0].IsPlaying);
        Assert.True(audio.Voices[1].IsPlaying);
    }

    [Fact]
    public void StopFadedOut_WithNoFade_DoesNothing()
    {
        var (audio, pool) = NewPool();
        pool.Play();

        pool.StopFadedOut(default);

        Assert.True(audio.Voices[0].IsPlaying);
    }

    [Fact]
    public void FadeOutAll_ReturnsAFadeThatIsNeverZero()
    {
        var (_, pool) = NewPool();

        Assert.NotEqual(0, pool.FadeOutVoices(FadeTime));
    }

    // Stopping

    [Fact]
    public void StopAll_StopsEveryVoice_AndEndsItsFade()
    {
        var (audio, pool) = NewPool();
        pool.Play();
        var fade = pool.FadeOutVoices(FadeTime);
        pool.Play();

        pool.StopAll();

        Assert.All(audio.Voices, v => Assert.False(v.IsPlaying));

        // A later play of that voice isn't stopped by the old fade
        audio.Voices[0].Play();
        pool.StopFadedOut(fade);
        Assert.True(audio.Voices[0].IsPlaying);
    }

    // Disposing

    [Fact]
    public void Dispose_DisposesEveryVoice()
    {
        var (audio, pool) = NewPool();
        pool.Play();
        pool.Play();

        pool.Dispose();

        Assert.All(audio.Voices, v => Assert.True(v.IsDisposed));
    }

    [Fact]
    public void Dispose_Twice_IsFine()
    {
        var (_, pool) = NewPool();

        pool.Dispose();
        pool.Dispose();
    }

    [Fact]
    public void AfterDispose_PlayAndFadeOutAll_Throw()
    {
        var (_, pool) = NewPool();
        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pool.Play());
        Assert.Throws<ObjectDisposedException>(() => pool.Play(0.5f));
        Assert.Throws<ObjectDisposedException>(() => pool.FadeOutVoices(FadeTime));
    }

    // Review finding T12
    [Fact]
    public void AfterDispose_StopAllAndStopFadedOut_DoNothing()
    {
        var (audio, pool) = NewPool();
        pool.Play();
        var fade = pool.FadeOutVoices(FadeTime);
        int stops = audio.Voices[0].Stops;

        pool.Dispose();
        pool.StopAll();
        pool.StopFadedOut(fade);

        Assert.Equal(stops, audio.Voices[0].Stops);
    }

    [Fact]
    public void Play_AfterTheSoundIsDisposed_Throws()
    {
        var audio = new FakeAudio();
        var sound = FakeAudio.CreateSound();
        var pool = new SoundPool(audio.Service, sound);
        pool.Play();

        ((IContent)sound).Dispose();

        Assert.Throws<ObjectDisposedException>(() => pool.Play());
    }

    // Groups

    [Fact]
    public void NoGroup_LeavesAVoiceAtFullVolumeWithNoControl()
    {
        var (audio, pool) = NewPool();

        var voice = pool.PlayVoice();

        Assert.Null(voice.VolumeControl);
        Assert.Empty(audio.VolumeControls);
    }

    private static SoundPool PoolUnder(
        FakeAudio audio,
        VolumeControl group,
        int voiceLimit = 1)
    {
        return new SoundPool(audio.Service, FakeAudio.CreateSound(), voiceLimit, group);
    }

    [Fact]
    public void Group_IsTheControlOfAVoiceWithNoneOfItsOwn_WithoutMakingOne()
    {
        var audio = new FakeAudio();
        var group = audio.Service.CreateVolumeControl();
        var made = audio.VolumeControls.Count;

        var pool = PoolUnder(audio, group);
        var voice = pool.PlayVoice();

        Assert.Same(group, pool.VolumeControl);
        Assert.Same(group, voice.VolumeControl);
        Assert.Equal(made, audio.VolumeControls.Count);
    }

    [Fact]
    public void Group_IsTheParentOfAVoicesOwnControl()
    {
        var audio = new FakeAudio();
        var group = audio.Service.CreateVolumeControl();

        var pool = PoolUnder(audio, group);
        pool.Play(0.5f);

        Assert.Same(FakeAudio.FakeOf(group), audio.VolumeControls[^1].Parent);
    }

    [Fact]
    public void Group_ReachesAVoiceCreatedLater()
    {
        var audio = new FakeAudio();
        var group = audio.Service.CreateVolumeControl();
        var pool = PoolUnder(audio, group, voiceLimit: 2);

        var first = pool.PlayVoice();
        var second = pool.PlayVoice();

        Assert.NotSame(first, second);
        Assert.Same(group, second.VolumeControl);
    }

    [Fact]
    public void Dispose_LeavesTheGroupForItsOtherPools()
    {
        var audio = new FakeAudio();
        var group = audio.Service.CreateVolumeControl();
        var pool = PoolUnder(audio, group);
        pool.Play();

        pool.Dispose();

        group.Volume = 0.5f;
        Assert.Equal(0.5f, group.Volume);
    }

    [Fact]
    public void Disposing_the_pool_lets_a_disposed_group_go()
    {
        var audio = new FakeAudio();
        var group = audio.Service.CreateVolumeControl();
        var pool = PoolUnder(audio, group);
        pool.Play();
        group.Dispose();
        Assert.False(FakeAudio.IsReleased(group));

        pool.Dispose();

        Assert.True(FakeAudio.IsReleased(group));
    }
}
