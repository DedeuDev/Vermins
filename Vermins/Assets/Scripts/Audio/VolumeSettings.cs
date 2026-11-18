using UnityEngine;
using UnityEngine.Audio;

public enum AudioChannel
{
    Master,
    Music,
    SFX,
    Ambience,
    UI
}

/// <summary>
/// Volume dos canais do mixer em PlayerPrefs (0 a 1 na UI, dB no mixer).
///
/// Exemplo de uso nos sliders da tela de Options (do Rogger):
///   sliderMaster.SetValueWithoutNotify(VolumeSettings.GetVolume(AudioChannel.Master));
///   sliderMaster.onValueChanged.AddListener(v => VolumeSettings.SetVolume(AudioChannel.Master, v));
///   sliderMusic.SetValueWithoutNotify(VolumeSettings.GetVolume(AudioChannel.Music));
///   sliderMusic.onValueChanged.AddListener(v => VolumeSettings.SetVolume(AudioChannel.Music, v));
///   sliderSfx.SetValueWithoutNotify(VolumeSettings.GetVolume(AudioChannel.SFX));
///   sliderSfx.onValueChanged.AddListener(v => VolumeSettings.SetVolume(AudioChannel.SFX, v));
///   sliderAmbience.SetValueWithoutNotify(VolumeSettings.GetVolume(AudioChannel.Ambience));
///   sliderAmbience.onValueChanged.AddListener(v => VolumeSettings.SetVolume(AudioChannel.Ambience, v));
///   sliderUi.SetValueWithoutNotify(VolumeSettings.GetVolume(AudioChannel.UI));
///   sliderUi.onValueChanged.AddListener(v => VolumeSettings.SetVolume(AudioChannel.UI, v));
/// </summary>
public static class VolumeSettings
{
    private const float DefaultVolume = 1f;
    private const float MinLinearVolume = 0.0001f;

    // Deixei tabelas fixas pra nao montar string a cada arrasto de slider.
    private static readonly string[] PrefKeys =
    {
        "Vermins.Audio.Master",
        "Vermins.Audio.Music",
        "Vermins.Audio.SFX",
        "Vermins.Audio.Ambience",
        "Vermins.Audio.UI"
    };

    private static readonly string[] MixerParams =
    {
        "MasterVolume",
        "MusicVolume",
        "SFXVolume",
        "AmbienceVolume",
        "UIVolume"
    };

    public static void SetVolume(AudioChannel channel, float volume)
    {
        int index = (int)channel;
        if (index < 0 || index >= PrefKeys.Length)
            return;

        float clamped = Mathf.Clamp01(volume);

        // Nao chamo PlayerPrefs.Save aqui porque o slider chama todo frame
        // enquanto arrasta; a Unity grava quando o jogo fecha.
        PlayerPrefs.SetFloat(PrefKeys[index], clamped);

        AudioManager manager = AudioManager.Instance;
        if (manager != null && manager.Library != null)
            ApplyChannel(manager.Library.Mixer, index, clamped);
    }

    public static float GetVolume(AudioChannel channel)
    {
        int index = (int)channel;
        if (index < 0 || index >= PrefKeys.Length)
            return DefaultVolume;

        return Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeys[index], DefaultVolume));
    }

    public static float ToDecibels(float linearVolume)
    {
        return Mathf.Log10(Mathf.Max(linearVolume, MinLinearVolume)) * 20f;
    }

    public static void ApplyAll(AudioMixer mixer)
    {
        if (mixer == null)
            return;

        for (int i = 0; i < PrefKeys.Length; i++)
        {
            float linear = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeys[i], DefaultVolume));
            ApplyChannel(mixer, i, linear);
        }
    }

    private static void ApplyChannel(AudioMixer mixer, int index, float linear)
    {
        if (mixer == null)
            return;

        mixer.SetFloat(MixerParams[index], ToDecibels(linear));
    }
}
