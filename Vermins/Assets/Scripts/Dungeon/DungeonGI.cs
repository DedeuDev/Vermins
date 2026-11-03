using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// Arquivo do Ian. Reaproveitei a foto de cada sala como um rebote SH L2:
/// piso e parede compartilham a captura, mas recebem luz por direcao.
/// Li as seis faces uma vez na carga, em linear, sem recapturar nem Update.
/// Somei o rebote ao ambiente do tema para nao apagar a luz escolhida.
/// </summary>
[RequireComponent(typeof(DungeonReflections))]
public class DungeonGI : MonoBehaviour
{
    private DungeonReflections reflections;
    private DungeonTheme theme;
    private SphericalHarmonicsL2[] roomLighting;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<LightProbeUsage> originalUsage = new List<LightProbeUsage>();
    private readonly List<int> roomIndices = new List<int>();
    private MaterialPropertyBlock block;
    private readonly SphericalHarmonicsL2[] singleProbe = new SphericalHarmonicsL2[1];
    private bool giEnabled = true;

    public bool Ready { get; private set; }
    public int AppliedRendererCount => renderers.Count;
    public int XZAssignedRendererCount { get; private set; }
    public int NearestAssignedRendererCount { get; private set; }
    public string[] UnassignedRenderers { get; private set; } = System.Array.Empty<string>();
    public int ReadResolution { get; private set; }
    public int ReadMip { get; private set; }
    public double LoadCpuMilliseconds { get; private set; }
    public double LoadElapsedMilliseconds { get; private set; }
    public int LoadFrames { get; private set; }

    public bool GIEnabled
    {
        get => giEnabled;
        set
        {
            giEnabled = value;
            if (Ready) ApplyLighting();
        }
    }

    private IEnumerator Start()
    {
        block = new MaterialPropertyBlock();
        reflections = GetComponent<DungeonReflections>();
        theme = GetComponent<DungeonTheme>();
        while (!reflections.CaptureComplete)
            yield return null;

        long started = Stopwatch.GetTimestamp();
        int firstFrame = Time.frameCount;
        if (!SystemInfo.supportsAsyncGPUReadback)
        {
            Debug.LogError("DungeonGI: AsyncGPUReadback indisponivel.", this);
            yield break;
        }

        long cpuStarted = Stopwatch.GetTimestamp();
        roomLighting = new SphericalHarmonicsL2[reflections.ProbeCount];
        LoadCpuMilliseconds += MillisecondsSince(cpuStarted);
        for (int i = 0; i < roomLighting.Length; i++)
        {
            cpuStarted = Stopwatch.GetTimestamp();
            Texture texture = reflections.GetProbe(i).realtimeTexture;
            if (texture == null)
            {
                Debug.LogError("DungeonGI: captura sem textura.", this);
                yield break;
            }
            int mip = 0;
            while ((texture.width >> mip) > 16 && mip + 1 < texture.mipmapCount)
                mip++;
            ReadMip = mip;
            ReadResolution = Mathf.Max(1, texture.width >> mip);
            var request = AsyncGPUReadback.Request(texture, mip, TextureFormat.RGBAFloat);
            LoadCpuMilliseconds += MillisecondsSince(cpuStarted);
            while (!request.done)
                yield return null;
            cpuStarted = Stopwatch.GetTimestamp();
            if (request.hasError || request.layerCount != 6)
            {
                Debug.LogError("DungeonGI: falhou a leitura das seis faces.", this);
                yield break;
            }

            var faces = new Color[6][];
            for (int face = 0; face < 6; face++)
                faces[face] = request.GetData<Color>(face).ToArray();
            // Se nao houver mip, reduzi a CPU em blocos pesados por angulo
            // solido. Nao usei media simples, que superestima os cantos.
            roomLighting[i] = ProjectCubemap(faces, ReadResolution);
            LoadCpuMilliseconds += MillisecondsSince(cpuStarted);
        }

        cpuStarted = Stopwatch.GetTimestamp();
        AssignRenderers();
        Ready = true;
        ApplyLighting();
        LoadCpuMilliseconds += MillisecondsSince(cpuStarted);
        LoadElapsedMilliseconds = MillisecondsSince(started);
        LoadFrames = Time.frameCount - firstFrame;
    }

    private static double MillisecondsSince(long start) =>
        (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;

    // Integrei luzes direcionais com o cosseno da propria Unity. Calibrei
    // o termo constante contra AddAmbientLight: C entra e C sai, sem pi
    // extra nem conversao sRGB sobre a radiancia HDR que ja veio linear.
    public static SphericalHarmonicsL2 ProjectCubemap(Color[][] faces, int size)
    {
        var result = new SphericalHarmonicsL2();
        var white = new SphericalHarmonicsL2();
        int reducedSize = Mathf.Min(size, 16);
        for (int face = 0; face < 6; face++)
            for (int y = 0; y < reducedSize; y++)
                for (int x = 0; x < reducedSize; x++)
                {
                    Color radiance = Color.clear;
                    float weight = 0f;
                    int x0 = x * size / reducedSize;
                    int x1 = (x + 1) * size / reducedSize;
                    int y0 = y * size / reducedSize;
                    int y1 = (y + 1) * size / reducedSize;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++)
                        {
                            float area = TexelSolidAngle(sx, sy, size);
                            radiance += faces[face][sy * size + sx] * area;
                            weight += area;
                        }
                    Vector3 direction = FaceDirection(face,
                        (x + 0.5f) * 2f / reducedSize - 1f,
                        (y + 0.5f) * 2f / reducedSize - 1f);
                    result.AddDirectionalLight(direction, radiance, 1f);
                    white.AddDirectionalLight(direction, Color.white, weight);
                }
        var ambient = new SphericalHarmonicsL2();
        ambient.AddAmbientLight(Color.white);
        return result * (ambient[0, 0] / white[0, 0]);
    }

    private static float TexelSolidAngle(int x, int y, int size)
    {
        float x0 = x * 2f / size - 1f;
        float y0 = y * 2f / size - 1f;
        float x1 = (x + 1) * 2f / size - 1f;
        float y1 = (y + 1) * 2f / size - 1f;
        return Area(x1, y1) - Area(x0, y1) - Area(x1, y0) + Area(x0, y0);
    }

    private static float Area(float x, float y) =>
        Mathf.Atan2(x * y, Mathf.Sqrt(x * x + y * y + 1f));

    private static Vector3 FaceDirection(int face, float u, float v)
    {
        // Corrigi o v: a leitura GPU traz as linhas de baixo para cima.
        // Testei as metades inferiores de +X e +Z contra as duas diagonais.
        v = -v;
        switch ((CubemapFace)face)
        {
            case CubemapFace.PositiveX: return new Vector3(1f, -v, -u).normalized;
            case CubemapFace.NegativeX: return new Vector3(-1f, -v, u).normalized;
            case CubemapFace.PositiveY: return new Vector3(u, 1f, v).normalized;
            case CubemapFace.NegativeY: return new Vector3(u, -1f, -v).normalized;
            case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f).normalized;
            default: return new Vector3(-u, -v, -1f).normalized;
        }
    }

    private void AssignRenderers()
    {
        var unassigned = new List<string>();
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (renderer.gameObject.scene != gameObject.scene ||
                renderer is ParticleSystemRenderer || renderer is LineRenderer ||
                renderer.GetComponentInParent<Health>() != null || IsPlayer(renderer.transform))
                continue;
            int room = -1;
            int nearestRoom = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < reflections.ProbeCount; i++)
            {
                BoxCollider bounds = reflections.GetModule(i).PlacementBounds;
                Vector3 local = bounds.transform.InverseTransformPoint(renderer.bounds.center) - bounds.center;
                Vector3 half = bounds.size * 0.5f;
                if (Mathf.Abs(local.x) <= half.x + 0.001f &&
                    Mathf.Abs(local.z) <= half.z + 0.001f)
                {
                    room = i;
                    XZAssignedRendererCount++;
                    break;
                }
                // Ignorei a altura da caixa de 1 m; medi a borda em metros
                // do mundo para cobrir as emendas ate 2 m, mesmo com escala.
                Vector3 edge = bounds.transform.TransformPoint(bounds.center + new Vector3(
                    Mathf.Clamp(local.x, -half.x, half.x), 0f,
                    Mathf.Clamp(local.z, -half.z, half.z)));
                Vector3 delta = renderer.bounds.center - edge;
                float distance = delta.x * delta.x + delta.z * delta.z;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestRoom = i;
                }
            }
            if (room < 0 && nearestRoom >= 0 && nearestDistance <= 4f)
            {
                room = nearestRoom;
                NearestAssignedRendererCount++;
            }
            if (room < 0)
            {
                string path = renderer.name;
                for (Transform parent = renderer.transform.parent; parent != null; parent = parent.parent)
                    path = parent.name + "/" + path;
                unassigned.Add(path + " (" + renderer.GetType().Name + ")");
                continue;
            }
            renderers.Add(renderer);
            originalUsage.Add(renderer.lightProbeUsage);
            roomIndices.Add(room);
        }
        UnassignedRenderers = unassigned.ToArray();
    }

    private static bool IsPlayer(Transform current)
    {
        for (; current != null; current = current.parent)
            if (current.CompareTag("Player")) return true;
        return false;
    }

    private void ApplyLighting()
    {
        // Li depois do DungeonTheme. Mantive o ambiente na captura para
        // nao piscar; o rebote conta albedo x ambiente uma segunda vez.
        SphericalHarmonicsL2 ambient = RenderSettings.ambientProbe;
        float strength = theme != null ? theme.GIStrength : 1f;
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (!giEnabled || !isActiveAndEnabled)
            {
                renderer.lightProbeUsage = originalUsage[i];
                renderer.SetPropertyBlock(null);
                continue;
            }
            singleProbe[0] = ambient + roomLighting[roomIndices[i]] * strength;
            block.Clear();
            block.CopySHCoefficientArraysFrom(singleProbe);
            renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
            renderer.SetPropertyBlock(block);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (Ready) ApplyLighting();
    }

    private void OnEnable()
    {
        if (Ready) ApplyLighting();
    }
}
