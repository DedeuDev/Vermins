using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Arquivo do Ian. Da a cada dungeon a sua cara: o material dos modulos,
/// a cor do ambiente e o fundo de fora das paredes. A dungeon ficava
/// repetitiva, e as duas eram iguais: os 14 modulos, o bloqueio
/// de porta e o PilarDaPorta usavam o Lit padrao da URP, o mesmo cinza, e
/// a luz das duas cenas era a mesma.
///
/// O material sozinho quase nao mudava nada. Comparei fotos no mesmo
/// lugar, e a luz laranja da tocha cobre a cor da parede. O que muda a
/// cara e o ambiente, que pinta toda a parte sem tocha, e o fundo: fora
/// das paredes a camera via o chao mostarda do ceu padrao, uns 30% da
/// tela. Testei neblina antes e ela nao aparecia: a camera fica a 24 m
/// de tudo, entao so tingia a tela por igual.
///
/// As duas cenas usam o mesmo DungeonGenerator.prefab. O padrao do prefab
/// e o dos Esgotos Superiores, e a DungeonComPlayer2 sobrescreve o
/// material e o ambiente, como a DungeonComPlayer ja sobrescreve o numero
/// de salas.
///
/// Ambiente: sem luz nenhuma alem do ceu, a tocha nao aparecia. Troco o
/// ambiente do ceu por uma cor so, escura, enquanto este componente
/// existir, e devolvo o original no OnDestroy. Antes isso morava no
/// DungeonGuide, como 20% do ceu; passou pra ca pra so um script escrever
/// o ambiente.
///
/// Reflexo: o ceu padrao continuava refletindo com intensidade 1, e tudo
/// que e liso (inimigo e boss no Lit, a armadura do Player) ganhava um
/// contorno cinza-azulado. Numa dungeon fechada o reflexo seria das
/// proprias paredes, quase pretas, entao zero o reflexo do ceu enquanto
/// este componente existir. O brilho de superficie lisa fica so o das
/// tochas.
///
/// Luz do Player: com o ambiente escuro e sem o reflexo do ceu, o que
/// ficava longe da tocha virava preto. Em vez de clarear a dungeon toda
/// (a tocha perderia forca como guia), ponho uma luz quente em volta do
/// Player, sem sombra, como o raio de luz do Diablo. Primeiro ela ficou
/// 3 m em cima dele, e o Player continuava preto: a camera ve a frente
/// dele, e a luz so pegava o topo dos ombros. Agora ela e filha da
/// camera, que nao gira e segue o Player a distancia fixa, e fica uns
/// metros antes dele, do lado de quem olha. Anda junto sem Update e fica
/// so na dungeon: nem o Player.prefab nem a camera mudam.
///
/// Fundo: a camera e o CameraDoPlayer.prefab, o mesmo da HUB, que precisa
/// do ceu. Em vez de sobrescrever o prefab na cena, ponho a camera desta
/// cena em cor solida.
///
/// Material: so troca quem usa o replacedMaterial e mora dentro de um
/// DungeonModule (o bloqueio de porta nasce dentro do socket) ou no
/// "Generated Seams". O boss placeholder e os inimigos tambem sao Lit,
/// mas nascem em outra raiz e ficam como estao. Cada modulo e uma malha
/// so com um material, entao piso e parede ficam da mesma cor.
///
/// O ambiente e o fundo entram no Start, antes do primeiro frame. O
/// material espera o NavMesh, como o DungeonGuide. Nao tem Update.
/// </summary>
[RequireComponent(typeof(RuntimeDungeonNavMesh))]
public class DungeonTheme : MonoBehaviour
{
    [Header("Material")]
    [Tooltip("O cinza que os modulos trazem: o Lit.mat padrao da URP. So " +
             "quem usa este troca.")]
    [SerializeField] private Material replacedMaterial;

    [Tooltip("O material desta dungeon. Vazio, os modulos ficam cinza.")]
    [SerializeField] private Material moduleMaterial;

    [Header("Luz")]
    [Tooltip("A luz que sobra onde a tocha nao chega. O seletor e em sRGB, " +
             "igual ao do Lighting. Mais claro apaga a tocha.")]
    [SerializeField] private Color ambientColor = new Color(0.272f, 0.235f, 0.206f);

    [Tooltip("A forca do rebote por sala somado ao ambiente deste tema.")]
    [SerializeField, Min(0f)] private float giStrength = 1f;

    [Tooltip("A cor de fora das paredes, onde a camera via o ceu.")]
    [SerializeField] private Color outsideColor = Color.black;

    [Header("Luz do Player")]
    [Tooltip("Alcance da luz em volta do Player, em metros. Zero desliga.")]
    [SerializeField, Min(0f)] private float playerLightRange = 8f;

    [Tooltip("Medi 1,5 e a sala sem tocha mal aparecia; com 3 ela aparece " +
             "e a tocha continua mais forte.")]
    [SerializeField, Min(0f)] private float playerLightIntensity = 3f;

    [SerializeField] private Color playerLightColor = new Color(1f, 0.82f, 0.62f);

    [Tooltip("Quantos metros a luz fica antes do Player, na linha da camera. " +
             "Com 3 e a camera a 55 graus, ela fica 2,5 m acima e 1,7 m a " +
             "frente dele.")]
    [SerializeField, Min(0f)] private float playerLightPullBack = 3f;

    [Tooltip("Maximo de frames esperando o NavMesh, igual aos spawners.")]
    [SerializeField, Min(1)] private int maxFramesWaitingForNavMesh = 300;

    // Nome da raiz que o DungeonSeams cria pros pilares.
    private const string SeamsRootName = "Generated Seams";

    private RuntimeDungeonNavMesh runtimeNavMesh;
    private SphericalHarmonicsL2 originalAmbient;
    private float originalReflection;
    private bool lightChanged;
    private Light playerLight;

    /// <summary>Quantas malhas trocaram de material. O teste le daqui.</summary>
    public int PaintedCount { get; private set; }

    public float GIStrength => giStrength;

    private void Awake()
    {
        runtimeNavMesh = GetComponent<RuntimeDungeonNavMesh>();
    }

    private void Start()
    {
        ApplyLight();
        StartCoroutine(PaintWhenReady());
    }

    private void OnDestroy()
    {
        if (lightChanged)
        {
            RenderSettings.ambientProbe = originalAmbient;
            RenderSettings.reflectionIntensity = originalReflection;
        }

        if (playerLight != null)
            Destroy(playerLight.gameObject);
    }

    /// <summary>A luz em volta do Player, ou null. O teste le daqui.</summary>
    public Light PlayerLight => playerLight;

    private void ApplyLight()
    {
        originalAmbient = RenderSettings.ambientProbe;

        // O Lighting passa a cor pra linear antes de montar o ambiente, e
        // o projeto e linear. Faco igual, pra a cor do Inspector valer o
        // mesmo que la.
        Color cor = QualitySettings.activeColorSpace == ColorSpace.Linear
            ? ambientColor.linear
            : ambientColor;

        SphericalHarmonicsL2 plano = new SphericalHarmonicsL2();
        plano.AddAmbientLight(cor);
        RenderSettings.ambientProbe = plano;

        originalReflection = RenderSettings.reflectionIntensity;
        RenderSettings.reflectionIntensity = 0f;
        lightChanged = true;

        Camera camera = Camera.main;

        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = outsideColor;
        }

        AddPlayerLight(camera);
    }

    private void AddPlayerLight(Camera camera)
    {
        IsometricCameraFollow segue = camera != null ? camera.GetComponent<IsometricCameraFollow>() : null;

        if (segue == null || playerLightRange <= 0f)
            return;

        GameObject luz = new GameObject("Luz do Player");
        luz.transform.SetParent(camera.transform, false);
        luz.transform.localPosition = Vector3.forward * Mathf.Max(0f, segue.Distance - playerLightPullBack);

        playerLight = luz.AddComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.range = playerLightRange;
        playerLight.intensity = playerLightIntensity;
        playerLight.color = playerLightColor;

        // Sem sombra de proposito: cada luz pontual com sombra pede 6 mapas,
        // e o atlas ja e das tochas.
        playerLight.shadows = LightShadows.None;
    }

    private IEnumerator PaintWhenReady()
    {
        int esperou = 0;

        while (!runtimeNavMesh.HasBuiltNavMesh && esperou < maxFramesWaitingForNavMesh)
        {
            esperou++;
            yield return null;
        }

        if (!runtimeNavMesh.HasBuiltNavMesh)
        {
            Debug.LogError("DungeonTheme: o NavMesh nao ficou pronto.", this);
            yield break;
        }

        // O DungeonSeams poe os pilares no mesmo frame em que o NavMesh
        // fica pronto, e nao sei quem de nos dois acorda primeiro. Um
        // frame de folga garante que eles ja existem.
        yield return null;

        Paint();
    }

    [ContextMenu("Repaint")]
    private void Paint()
    {
        PaintedCount = 0;

        if (replacedMaterial == null || moduleMaterial == null)
            return;

        foreach (DungeonModule modulo in GetComponentsInChildren<DungeonModule>())
            PaintUnder(modulo.transform);

        Transform pilares = transform.Find(SeamsRootName);

        if (pilares != null)
            PaintUnder(pilares);
    }

    private void PaintUnder(Transform raiz)
    {
        foreach (MeshRenderer malha in raiz.GetComponentsInChildren<MeshRenderer>())
        {
            if (malha.sharedMaterial != replacedMaterial)
                continue;

            malha.sharedMaterial = moduleMaterial;
            PaintedCount++;
        }
    }
}
