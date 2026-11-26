using System;
using UnityEngine;

// Whether sound effects and music play, kept between sittings. Settings
// (T-040) switch them; JuicyAudioHooks and JuicyMusic read them.
public static class AudioPrefs
{
    const string SoundKey = "Juicy.SoundOn";
    const string MusicKey = "Juicy.MusicOn";

    public static event Action Changed;

    public static bool SoundOn
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set => Set(SoundKey, value);
    }

    public static bool MusicOn
    {
        get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
        set => Set(MusicKey, value);
    }

    static void Set(string key, bool on)
    {
        PlayerPrefs.SetInt(key, on ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
