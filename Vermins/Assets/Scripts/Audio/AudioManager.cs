using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Central de audio do jogo: pool de fontes 2D/3D, musica com crossfade,
/// ouvinte 3D preso no Player e som padrao de dano/morte dos inimigos.
///
/// Nasce sozinho antes da primeira cena pra nenhuma cena nem prefab de
/// colega precisar de objeto de audio. Como a camera isometrica fica a
/// mais de 24 m do chao, deixar o AudioListener nela deixava tudo longe e
/// abafado; aqui eu crio um ouvinte proprio 1,5 m acima do Player, virado
/// so pelo giro em Y da camera pra esquerda e direita baterem com a tela.
/// </summary>
[DefaultExecutionOrder(100)]
public class AudioManager : MonoBehaviour
{
    private const int PoolSize = 24;
    private const float MusicCrossfadeDuration = 1.5f;
    private const float ListenerHeightOffset = 1.5f;

    private static AudioManager instance;

    private AudioLibrary library;
    private AudioMixerGroup musicFallbackGroup;

    private AudioSource[] pool;
    private ulong[] poolOrder;
    private ulong playSequence;
    private int playCount;

    private AudioSource[] musicSources;
    private int activeMusicIndex;
    private SoundCue currentMusicCue;
    private AudioClip currentMusicClip;
    private float outgoingStartVolume;
    private float targetMusicVolume;
    private float crossfadeElapsed;
    private bool isCrossfading;

    private Transform listenerTransform;
    private AudioListener managerListener;
    private Transform cachedPlayer;
    private Camera cachedCamera;
    private bool handledInitialScene;

    // Guardo o resultado do TryGetComponent por instancia pra nao buscar
    // componente a cada golpe quando varios inimigos apanham juntos.
    private readonly Dictionary<int, HealthAudio> healthAudioCache = new Dictionary<int, HealthAudio>(64);

    public static AudioManager Instance => instance;
    public AudioLibrary Library => library;
    public int PlayCount => playCount;

    // Zera o singleton caso o editor entre em Play sem reload de dominio.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        AudioManager existing = FindFirstObjectByType<AudioManager>();
        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return;
        }

        var root = new GameObject("AudioManager");
        root.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        library = Resources.Load<AudioLibrary>("AudioLibrary");
        AudioMixer mixer = library != null ? library.Mixer : null;

        if (mixer != null)
        {
            AudioMixerGroup[] musicGroups = mixer.FindMatchingGroups("Music");
            if (musicGroups != null && musicGroups.Length > 0)
                musicFallbackGroup = musicGroups[0];
        }

        CreateListener();
        CreatePool();
        CreateMusicSources();

        VolumeSettings.ApplyAll(mixer);
    }

    private void Start()
    {
        // Reaplico aqui alem do Awake porque o mixer pode inicializar o
        // snapshot padrao entre o Awake e o primeiro quadro.
        AudioMixer mixer = library != null ? library.Mixer : null;
        VolumeSettings.ApplyAll(mixer);

        if (!handledInitialScene)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
                ApplySceneSetup(activeScene);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Health.AnyDamaged += HandleAnyHealthDamaged;
        Health.AnyDied += HandleAnyHealthDied;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Health.AnyDamaged -= HandleAnyHealthDamaged;
        Health.AnyDied -= HandleAnyHealthDied;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void Play(SoundCue cue)
    {
        PlayInternal(cue, Vector3.zero, false);
    }

    public void PlayAt(SoundCue cue, Vector3 position)
    {
        PlayInternal(cue, position, true);
    }

    private void PlayInternal(SoundCue cue, Vector3 position, bool allowSpatial)
    {
        if (cue == null || !cue.HasClip)
            return;

        if (!cue.TryConsumeCooldown(Time.unscaledTime))
            return;

        AudioClip clip = cue.PickClip();
        if (clip == null)
            return;

        int slot = AcquirePoolSlot();
        AudioSource source = pool[slot];

        if (source.isPlaying)
            source.Stop();

        source.transform.position = position;
        source.clip = clip;
        source.volume = cue.SampleVolume();
        source.pitch = cue.SamplePitch();
        source.outputAudioMixerGroup = cue.Output;
        source.dopplerLevel = 0f;

        if (allowSpatial && cue.Spatial)
        {
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = cue.MinDistance;
            source.maxDistance = Mathf.Max(cue.MinDistance, cue.MaxDistance);
        }
        else
        {
            source.spatialBlend = 0f;
        }

        source.Play();
        poolOrder[slot] = ++playSequence;
        playCount++;
    }

    private int AcquirePoolSlot()
    {
        int oldestIndex = 0;
        ulong oldestOrder = ulong.MaxValue;

        for (int i = 0; i < PoolSize; i++)
        {
            if (!pool[i].isPlaying)
                return i;

            if (poolOrder[i] < oldestOrder)
            {
                oldestOrder = poolOrder[i];
                oldestIndex = i;
            }
        }

        return oldestIndex;
    }

    private void CreateListener()
    {
        var listenerObject = new GameObject("AudioListener");
        listenerObject.transform.SetParent(transform, false);
        listenerTransform = listenerObject.transform;
        managerListener = listenerObject.AddComponent<AudioListener>();
    }

    private void CreatePool()
    {
        pool = new AudioSource[PoolSize];
        poolOrder = new ulong[PoolSize];

        for (int i = 0; i < PoolSize; i++)
        {
            var sourceObject = new GameObject("SFX_" + i);
            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            pool[i] = source;
        }
    }

    private void CreateMusicSources()
    {
        musicSources = new AudioSource[2];

        for (int i = 0; i < 2; i++)
        {
            var musicObject = new GameObject("Music_" + i);
            musicObject.transform.SetParent(transform, false);

            AudioSource source = musicObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = musicFallbackGroup;
            musicSources[i] = source;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneSetup(scene);
    }

    private void ApplySceneSetup(Scene scene)
    {
        handledInitialScene = true;
        healthAudioCache.Clear();

        DisableOtherAudioListeners();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        cachedPlayer = playerObject != null ? playerObject.transform : null;
        cachedCamera = Camera.main;

        UpdateListenerTransform();

        if (library != null && library.TryGetMusic(scene.name, out SoundCue nextMusic))
            TransitionMusic(nextMusic);
    }

    private void DisableOtherAudioListeners()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != managerListener)
                listeners[i].enabled = false;
        }

        if (managerListener != null)
            managerListener.enabled = true;
    }

    private void TransitionMusic(SoundCue nextMusic)
    {
        if (nextMusic == currentMusicCue)
            return;

        AudioClip nextClip = nextMusic != null && nextMusic.HasClip ? nextMusic.PickClip() : null;

        // Se outro cue apontar pro mesmo clipe que ja esta tocando, deixo
        // seguir sem cortar nem reiniciar.
        if (nextClip != null && nextClip == currentMusicClip && musicSources[activeMusicIndex].isPlaying)
        {
            currentMusicCue = nextMusic;
            return;
        }

        currentMusicCue = nextMusic;
        currentMusicClip = nextClip;

        int outgoing = activeMusicIndex;
        int incoming = 1 - activeMusicIndex;
        activeMusicIndex = incoming;

        AudioSource incomingSource = musicSources[incoming];
        if (incomingSource.isPlaying)
            incomingSource.Stop();

        if (nextClip != null)
        {
            incomingSource.clip = nextClip;
            incomingSource.outputAudioMixerGroup = nextMusic.Output != null ? nextMusic.Output : musicFallbackGroup;
            incomingSource.pitch = nextMusic.SamplePitch();
            incomingSource.volume = 0f;
            incomingSource.Play();
            targetMusicVolume = nextMusic.SampleVolume();
        }
        else
        {
            incomingSource.clip = null;
            targetMusicVolume = 0f;
        }

        outgoingStartVolume = musicSources[outgoing].isPlaying ? musicSources[outgoing].volume : 0f;
        crossfadeElapsed = 0f;
        isCrossfading = musicSources[outgoing].isPlaying || nextClip != null;
    }

    private void Update()
    {
        if (!isCrossfading)
            return;

        crossfadeElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(crossfadeElapsed / MusicCrossfadeDuration);

        int outgoing = 1 - activeMusicIndex;
        AudioSource outgoingSource = musicSources[outgoing];
        AudioSource incomingSource = musicSources[activeMusicIndex];

        if (outgoingSource.isPlaying)
            outgoingSource.volume = Mathf.Lerp(outgoingStartVolume, 0f, t);

        if (incomingSource.isPlaying)
            incomingSource.volume = Mathf.Lerp(0f, targetMusicVolume, t);

        if (t >= 1f)
        {
            isCrossfading = false;

            if (outgoingSource.isPlaying)
            {
                outgoingSource.Stop();
                outgoingSource.clip = null;
            }
        }
    }

    private void LateUpdate()
    {
        UpdateListenerTransform();
    }

    private void UpdateListenerTransform()
    {
        if (listenerTransform == null)
            return;

        if (cachedCamera == null)
            cachedCamera = Camera.main;

        Vector3 position = listenerTransform.position;

        if (cachedPlayer != null)
        {
            position = cachedPlayer.position;
            position.y += ListenerHeightOffset;
        }
        else if (cachedCamera != null)
        {
            position = cachedCamera.transform.position;
        }

        Quaternion rotation = cachedCamera != null
            ? Quaternion.Euler(0f, cachedCamera.transform.eulerAngles.y, 0f)
            : Quaternion.identity;

        listenerTransform.SetPositionAndRotation(position, rotation);
    }

    private void HandleAnyHealthDamaged(Health health, float amount)
    {
        if (health == null || health.IsDead || health.CompareTag("Player"))
            return;

        HealthAudio custom = GetHealthAudio(health);
        SoundCue cue = custom != null && custom.HurtCue != null
            ? custom.HurtCue
            : (library != null ? library.EnemyHurt : null);

        PlayAt(cue, health.transform.position);
    }

    private void HandleAnyHealthDied(Health health)
    {
        if (health == null || health.CompareTag("Player"))
            return;

        HealthAudio custom = GetHealthAudio(health);
        SoundCue cue = custom != null && custom.DeathCue != null
            ? custom.DeathCue
            : (library != null ? library.EnemyDeath : null);

        PlayAt(cue, health.transform.position);
    }

    private HealthAudio GetHealthAudio(Health health)
    {
        int id = health.GetInstanceID();

        if (!healthAudioCache.TryGetValue(id, out HealthAudio cached))
        {
            health.TryGetComponent(out cached);
            healthAudioCache[id] = cached;
        }

        return cached;
    }
}
