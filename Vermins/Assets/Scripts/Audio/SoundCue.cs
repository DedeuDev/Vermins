using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Um som do jogo: lista de clipes pra variar sem repetir o mesmo duas
/// vezes seguidas, faixa de volume e pitch, grupo do mixer e alcance 3D.
///
/// Deixei o Eduardo trocar e preencher aqui sem mexer em codigo. Cue sem
/// clipe fica quieto de proposito, sem erro nem aviso, pra gente poder
/// ligar todos os ganchos do jogo antes dos arquivos de som chegarem.
/// </summary>
[CreateAssetMenu(fileName = "NewSoundCue", menuName = "Vermins/Audio/Sound Cue")]
public class SoundCue : ScriptableObject
{
    [Header("Clipes")]
    [Tooltip("Sorteia um por vez, sem repetir o anterior quando houver mais de um.")]
    [SerializeField] private AudioClip[] clips;

    [Header("Variacao")]
    [SerializeField, Range(0f, 1f)] private float minVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] private float minPitch = 1f;
    [SerializeField, Range(0.1f, 3f)] private float maxPitch = 1f;

    [Header("Roteamento")]
    [Tooltip("Grupo do VerminsMixer. Vazio toca direto sem passar pelo mixer.")]
    [SerializeField] private AudioMixerGroup output;

    [Header("Espacializacao 3D")]
    [Tooltip("Ligado toca na posicao do mundo; desligado toca igual nos dois ouvidos.")]
    [SerializeField] private bool spatial;

    [SerializeField, Min(0.1f)] private float minDistance = 2f;
    [SerializeField, Min(0.1f)] private float maxDistance = 25f;

    [Header("Protecao")]
    [Tooltip("Intervalo minimo entre dois disparos deste cue. Evita estourar " +
             "o som quando varios inimigos apanham juntos.")]
    [SerializeField, Min(0f)] private float cooldown = 0.05f;

    [System.NonSerialized] private int lastClipIndex = -1;
    [System.NonSerialized] private float lastPlayTime = float.NegativeInfinity;

    public AudioClip[] Clips
    {
        get => clips;
        set => clips = value;
    }

    public AudioMixerGroup Output
    {
        get => output;
        set => output = value;
    }

    public bool Spatial
    {
        get => spatial;
        set => spatial = value;
    }

    public float MinDistance
    {
        get => minDistance;
        set => minDistance = Mathf.Max(0.1f, value);
    }

    public float MaxDistance
    {
        get => maxDistance;
        set => maxDistance = Mathf.Max(minDistance, value);
    }

    public float Cooldown
    {
        get => cooldown;
        set => cooldown = Mathf.Max(0f, value);
    }

    public float MinVolume
    {
        get => minVolume;
        set => minVolume = Mathf.Clamp01(value);
    }

    public float MaxVolume
    {
        get => maxVolume;
        set => maxVolume = Mathf.Clamp01(value);
    }

    public float MinPitch
    {
        get => minPitch;
        set => minPitch = Mathf.Clamp(value, 0.1f, 3f);
    }

    public float MaxPitch
    {
        get => maxPitch;
        set => maxPitch = Mathf.Clamp(value, 0.1f, 3f);
    }

    /// <summary>
    /// Confere se existe pelo menos um clipe preenchido, sem alocar nada.
    /// </summary>
    public bool HasClip
    {
        get
        {
            if (clips == null || clips.Length == 0)
                return false;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Reserva o disparo se o cooldown ja venceu. Quando o editor entra em
    /// Play sem reload de dominio, o relogio volta pro zero e fica menor
    /// que o ultimo uso; nesse caso libero o som normalmente.
    /// </summary>
    public bool TryConsumeCooldown(float now)
    {
        if (cooldown > 0f && now >= lastPlayTime && now - lastPlayTime < cooldown)
            return false;

        lastPlayTime = now;
        return true;
    }

    public AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0)
            return null;

        if (clips.Length == 1)
        {
            lastClipIndex = 0;
            return clips[0];
        }

        int index = Random.Range(0, clips.Length);

        if (index == lastClipIndex)
            index = (index + Random.Range(1, clips.Length)) % clips.Length;

        if (clips[index] == null)
        {
            // Se algum slot estiver vazio no Inspector, procuro outro
            // preenchido antes de desistir, sem alocar lista.
            for (int i = 0; i < clips.Length; i++)
            {
                int candidate = (index + i) % clips.Length;
                if (candidate != lastClipIndex && clips[candidate] != null)
                {
                    lastClipIndex = candidate;
                    return clips[candidate];
                }
            }

            if (lastClipIndex >= 0 && lastClipIndex < clips.Length && clips[lastClipIndex] != null)
                return clips[lastClipIndex];

            return null;
        }

        lastClipIndex = index;
        return clips[index];
    }

    public float SampleVolume()
    {
        float low = Mathf.Min(minVolume, maxVolume);
        float high = Mathf.Max(minVolume, maxVolume);
        return Mathf.Approximately(low, high) ? low : Random.Range(low, high);
    }

    public float SamplePitch()
    {
        float low = Mathf.Min(minPitch, maxPitch);
        float high = Mathf.Max(minPitch, maxPitch);
        return Mathf.Approximately(low, high) ? low : Random.Range(low, high);
    }

    private void OnValidate()
    {
        if (maxDistance < minDistance)
            maxDistance = minDistance;
    }
}
