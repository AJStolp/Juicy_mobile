using NUnit.Framework;
using UnityEngine;

// Every sound the game calls for, and every music loop, is in Resources.
public class AudioTests
{
    [Test]
    public void EverySoundEffectLoads()
    {
        foreach (string name in JuicyAudioHooks.ClipNames)
            Assert.IsNotNull(Resources.Load<AudioClip>("Audio/" + name), name);
    }

    [Test]
    public void EveryMusicLoopLoadsAndLoops()
    {
        foreach (string name in new[] { "music_map", "music_level", "music_vault" })
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/Music/" + name);
            Assert.IsNotNull(clip, name);
            Assert.Greater(clip.length, 10f, name + " is a loop of some length");
        }
    }

    [Test]
    public void SoundAndMusicSettingsAreKept()
    {
        bool sound = AudioPrefs.SoundOn;
        bool music = AudioPrefs.MusicOn;
        int changes = 0;
        System.Action count = () => changes++;
        AudioPrefs.Changed += count;
        try
        {
            AudioPrefs.SoundOn = false;
            AudioPrefs.MusicOn = false;
            Assert.IsFalse(AudioPrefs.SoundOn);
            Assert.IsFalse(AudioPrefs.MusicOn);
            Assert.AreEqual(2, changes);
        }
        finally
        {
            AudioPrefs.Changed -= count;
            AudioPrefs.SoundOn = sound;
            AudioPrefs.MusicOn = music;
        }
    }
}
