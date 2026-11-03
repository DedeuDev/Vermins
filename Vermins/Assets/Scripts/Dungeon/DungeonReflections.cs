using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// Arquivo do Ian. Pus um reflexo da propria sala em cada modulo para a
/// poca lisa refletir parede e tocha, mantendo o reflexo do ceu zerado.
/// Esperei quatro frames depois do NavMesh para capturar tambem os props.
/// Distribui as probes de 64 entre frames, sem Update nem recaptura ao
/// alternar o botao. Tirei os renderers dos inimigos durante a carga:
/// eles usam a mesma layer do cenario e nao podem ficar gravados na poca.
/// Medi o tempo de CPU das cameras de reflexo; nao e tempo de GPU.
/// </summary>
[RequireComponent(typeof(RuntimeDungeonNavMesh))]
public class DungeonReflections : MonoBehaviour
{
    [SerializeField, Min(16)] private int resolution = 64;
    [SerializeField, Min(1)] private int maxFramesWaitingForNavMesh = 300;

    private ReflectionProbe[] probes = System.Array.Empty<ReflectionProbe>();
    private DungeonModule[] modules = System.Array.Empty<DungeonModule>();
    private Renderer[] hiddenRenderers = System.Array.Empty<Renderer>();
    private bool[] rendererStates;
    private Light[] hiddenLights = System.Array.Empty<Light>();
    private bool[] lightStates;
    private bool probesEnabled = true;
    private bool capturing;
    private long cameraStart;
    private int measuredFrame = -1;
    private double frameMilliseconds;
    private bool originalRealtimeReflections;

    private void Awake()
    {
        // Encontrei realtimeReflectionProbes desligado nos dois niveis
        // de qualidade. Liguei so em memoria e devolvo ao sair da dungeon.
        originalRealtimeReflections = QualitySettings.realtimeReflectionProbes;
        QualitySettings.realtimeReflectionProbes = true;
    }

    public int ProbeCount => probes.Length;
    public double RenderMilliseconds { get; private set; }
    public double WorstRenderFrameMilliseconds { get; private set; }
    public int FirstCaptureFrame { get; private set; } = -1;
    public int LastCaptureFrame { get; private set; } = -1;
    public int RenderedProbeCount { get; private set; }
    public bool CaptureComplete { get; private set; }
    public ReflectionProbe GetProbe(int index) => probes[index];
    public DungeonModule GetModule(int index) => modules[index];

    public bool ProbesEnabled
    {
        get => probesEnabled;
        set
        {
            probesEnabled = value;
            // Mantive as probes ativas durante a captura para o botao
            // nao cancelar uma textura que ainda esta sendo preenchida.
            if (!capturing)
                ApplyEnabled();
        }
    }

    private IEnumerator Start()
    {
        var navMesh = GetComponent<RuntimeDungeonNavMesh>();
        int waitedFrames = 0;
        while (!navMesh.HasBuiltNavMesh && waitedFrames++ < maxFramesWaitingForNavMesh)
            yield return null;

        if (!navMesh.HasBuiltNavMesh)
        {
            Debug.LogError("DungeonReflections: o NavMesh nao ficou pronto.", this);
            yield break;
        }

        for (int i = 0; i < 4; i++)
            yield return null;

        modules = GetComponentsInChildren<DungeonModule>();
        probes = new ReflectionProbe[modules.Length];
        int[] renderIds = new int[modules.Length];
        for (int i = 0; i < modules.Length; i++)
        {
            BoxCollider bounds = modules[i].PlacementBounds;
            if (bounds == null)
            {
                Debug.LogError("DungeonReflections: modulo sem PlacementBounds.", modules[i]);
                yield break;
            }

            var probeObject = new GameObject("Room Reflection");
            probeObject.SetActive(false);
            probeObject.transform.SetParent(bounds.transform, false);
            probeObject.transform.position = bounds.transform.TransformPoint(bounds.center);
            probeObject.transform.rotation = Quaternion.identity;
            // Compensei a escala do modulo: size e center sao locais,
            // mas a caixa de influencia precisa medir quatro metros.
            probeObject.transform.localScale = Vector3.one;
            Vector3 scale = probeObject.transform.lossyScale;
            probeObject.transform.localScale = new Vector3(1f / Mathf.Abs(scale.x),
                1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
            var probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.boxProjection = true;
            Vector3 size = bounds.bounds.size;
            probe.size = new Vector3(size.x, 4f, size.z);
            probe.center = Vector3.zero;
            probe.resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 16, 2048);
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = Color.black;
            probe.cullingMask = ~0;
            probe.nearClipPlane = 0.1f;
            probe.farClipPlane = Mathf.Max(size.x, size.z) * 2f + 4f;
            probe.blendDistance = 0.5f;
            probes[i] = probe;
            probeObject.SetActive(true);
        }

        HideActors();
        capturing = true;
        RenderPipelineManager.beginCameraRendering += BeginCaptureCamera;
        RenderPipelineManager.endCameraRendering += EndCaptureCamera;
        try
        {
            for (int i = 0; i < probes.Length; i++)
            {
                if (i == 0) FirstCaptureFrame = Time.frameCount;
                LastCaptureFrame = Time.frameCount;
                renderIds[i] = probes[i].RenderProbe();
                RenderedProbeCount++;
                yield return null;
            }

            // Esperei o ID terminar antes de devolver os renderers:
            // RenderProbe agenda trabalho, nao garante conclusao na chamada.
            for (int i = 0; i < probes.Length; i++)
            {
                int waited = 0;
                while (!probes[i].IsFinishedRendering(renderIds[i]) && waited++ < 300)
                    yield return null;
                if (!probes[i].IsFinishedRendering(renderIds[i]))
                {
                    Debug.LogError("DungeonReflections: captura nao terminou.", this);
                    yield break;
                }
            }
            CaptureComplete = true;
        }
        finally
        {
            FinishCapture();
        }
    }

    private void HideActors()
    {
        var renderers = new HashSet<Renderer>();
        var lights = new HashSet<Light>();
        // Busquei todo Health da cena, sem depender de tags: inimigo e boss
        // sao Untagged na layer Default. So a raiz gerada deixava o Player
        // de fora, e ele ficava parado na poca da sala inicial. Roda uma vez.
        foreach (Health actor in FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true))
                renderers.Add(renderer);
            foreach (Light light in actor.GetComponentsInChildren<Light>(true))
                lights.Add(light);
        }
        // Tirei tambem a luz da camera: ela acompanha o Player, mas nao
        // e filha do Health e ficava gravada so na sala inicial.
        DungeonTheme theme = GetComponent<DungeonTheme>();
        if (theme != null && theme.PlayerLight != null)
            lights.Add(theme.PlayerLight);
        hiddenLights = new Light[lights.Count];
        lights.CopyTo(hiddenLights);
        lightStates = new bool[hiddenLights.Length];
        for (int i = 0; i < hiddenLights.Length; i++)
        {
            lightStates[i] = hiddenLights[i].enabled;
            hiddenLights[i].enabled = false;
        }
        hiddenRenderers = new Renderer[renderers.Count];
        renderers.CopyTo(hiddenRenderers);
        rendererStates = new bool[hiddenRenderers.Length];
        for (int i = 0; i < hiddenRenderers.Length; i++)
        {
            rendererStates[i] = hiddenRenderers[i].enabled;
            hiddenRenderers[i].enabled = false;
        }
    }

    private void BeginCaptureCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType == CameraType.Reflection)
            cameraStart = Stopwatch.GetTimestamp();
    }

    private void EndCaptureCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType != CameraType.Reflection) return;
        double milliseconds = (Stopwatch.GetTimestamp() - cameraStart) * 1000d / Stopwatch.Frequency;
        RenderMilliseconds += milliseconds;
        if (measuredFrame != Time.frameCount)
        {
            measuredFrame = Time.frameCount;
            frameMilliseconds = 0d;
        }
        frameMilliseconds += milliseconds;
        WorstRenderFrameMilliseconds = System.Math.Max(WorstRenderFrameMilliseconds, frameMilliseconds);
    }

    private void FinishCapture()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCaptureCamera;
        RenderPipelineManager.endCameraRendering -= EndCaptureCamera;
        for (int i = 0; i < hiddenRenderers.Length; i++)
            if (hiddenRenderers[i] != null)
                hiddenRenderers[i].enabled = rendererStates[i];
        hiddenRenderers = System.Array.Empty<Renderer>();
        for (int i = 0; i < hiddenLights.Length; i++)
            if (hiddenLights[i] != null)
                hiddenLights[i].enabled = lightStates[i];
        hiddenLights = System.Array.Empty<Light>();
        capturing = false;
        ApplyEnabled();
    }

    private void ApplyEnabled()
    {
        // Vi que enabled=false libera a realtimeTexture. Zerei a
        // intensidade para desligar o reflexo sem perder a captura.
        foreach (ReflectionProbe probe in probes)
            if (probe != null) probe.intensity = probesEnabled ? 1f : 0f;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        FinishCapture();
    }

    private void OnDestroy()
    {
        QualitySettings.realtimeReflectionProbes = originalRealtimeReflections;
    }
}
