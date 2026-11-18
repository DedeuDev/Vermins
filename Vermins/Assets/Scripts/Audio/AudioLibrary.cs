using System;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Catalogo central de audio carregado de Resources/AudioLibrary pelo
/// AudioManager. Guarda o mixer, a tabela de musica por cena e os cues
/// padrao usados quando nenhum cue especifico foi arrastado no objeto.
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Vermins/Audio/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Serializable]
    public struct SceneMusicEntry
    {
        public string sceneName;
        public SoundCue musicCue;
    }

    [Header("Mixer")]
    [SerializeField] private AudioMixer mixer;

    [Header("Musica por cena")]
    [Tooltip("Cenas fora desta tabela mantem a musica atual tocando.")]
    [SerializeField] private SceneMusicEntry[] sceneMusic;

    [Header("Cues padrao")]
    [SerializeField] private SoundCue enemyHurt;
    [SerializeField] private SoundCue enemyDeath;
    [SerializeField] private SoundCue playerHurt;
    [SerializeField] private SoundCue playerDeath;
    [SerializeField] private SoundCue swing;
    [SerializeField] private SoundCue hit;
    [SerializeField] private SoundCue dodge;
    [SerializeField] private SoundCue groundSlam;
    [SerializeField] private SoundCue shockWave;
    [SerializeField] private SoundCue levelUp;
    [SerializeField] private SoundCue gold;
    [SerializeField] private SoundCue potionDrink;
    [SerializeField] private SoundCue pickup;

    public AudioMixer Mixer => mixer;
    public SceneMusicEntry[] SceneMusic => sceneMusic;

    public SoundCue EnemyHurt => enemyHurt;
    public SoundCue EnemyDeath => enemyDeath;
    public SoundCue PlayerHurt => playerHurt;
    public SoundCue PlayerDeath => playerDeath;
    public SoundCue Swing => swing;
    public SoundCue Hit => hit;
    public SoundCue Dodge => dodge;
    public SoundCue GroundSlam => groundSlam;
    public SoundCue ShockWave => shockWave;
    public SoundCue LevelUp => levelUp;
    public SoundCue Gold => gold;
    public SoundCue PotionDrink => potionDrink;
    public SoundCue Pickup => pickup;

    /// <summary>
    /// Procura a cena na tabela sem alocar memoria. Devolve false se a
    /// cena nao estiver listada, pra musica anterior continuar tocando.
    /// </summary>
    public bool TryGetMusic(string sceneName, out SoundCue cue)
    {
        if (sceneMusic != null && !string.IsNullOrEmpty(sceneName))
        {
            for (int i = 0; i < sceneMusic.Length; i++)
            {
                if (string.Equals(sceneMusic[i].sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    cue = sceneMusic[i].musicCue;
                    return true;
                }
            }
        }

        cue = null;
        return false;
    }
}
