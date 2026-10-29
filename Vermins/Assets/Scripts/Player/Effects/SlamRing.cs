using UnityEngine;

/// <summary>
/// Anel no chao que mostra ate onde o golpe em area pegou. Sem ele o
/// jogador nao tinha como ler os 3 m: a espada desce perto do corpo e o
/// dano sai bem mais longe.
///
/// O anel e desenhado no raio que o GroundSlam usou, entao quem levou dano
/// e quem esta dentro do anel. Mudou o raio no Inspector, o anel acompanha.
///
/// Um objeto so, filho do Player, reaproveitado a cada uso: o LineRenderer
/// liga na pancada e desliga sozinho no fim. Os pontos ficam em arrays
/// prontos, entao o anel nao gera lixo nem quando aparece.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class SlamRing : MonoBehaviour
{
    [Tooltip("O golpe que este anel mostra. Vazio procura no Player.")]
    [SerializeField] private GroundSlam slam;

    [Tooltip("Pontos do circulo. 48 ja fica redondo num raio de 3 m.")]
    [SerializeField, Min(8)] private int segments = 48;

    [Tooltip("Raio em que o anel nasce, antes de crescer ate o do golpe.")]
    [SerializeField, Min(0f)] private float startRadius = 0.5f;

    [Tooltip("Segundos pra crescer ate o raio do golpe. Curto, pra ler " +
             "como pancada e nao como aviso.")]
    [SerializeField, Min(0.01f)] private float growTime = 0.15f;

    [Tooltip("Segundos pra sumir depois de chegar no raio.")]
    [SerializeField, Min(0.01f)] private float fadeTime = 0.35f;

    [Tooltip("Quanto acima do chao o anel fica, pra nao piscar dentro do " +
             "piso.")]
    [SerializeField, Min(0f)] private float groundOffset = 0.05f;

    // O centro do Player fica na altura da cintura; o chao e achado por um
    // raio pra baixo. Comeca dentro da capsula do Player, e raio que nasce
    // dentro de um collider nao acerta ele.
    private const float GroundProbe = 3f;

    private LineRenderer line;
    private Vector3[] circle;
    private Vector3[] points;
    private Color startColor;
    private Color endColor;

    private Vector3 center;
    private float radius;
    private float shownAt;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();

        if (slam == null)
            slam = GetComponentInParent<GroundSlam>();

        // O circulo de raio 1 sai uma vez aqui; por frame so multiplico.
        circle = new Vector3[segments];
        points = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angulo = i * Mathf.PI * 2f / segments;
            circle[i] = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
        }

        // Em espaco do mundo: o Player sai andando e o anel fica onde a
        // pancada caiu.
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = segments;
        startColor = line.startColor;
        endColor = line.endColor;
        line.enabled = false;
    }

    private void OnEnable()
    {
        if (slam != null)
            slam.OnImpact += Show;
    }

    private void OnDisable()
    {
        if (slam != null)
            slam.OnImpact -= Show;

        line.enabled = false;
    }

    private void Show(Vector3 centro, float raio)
    {
        center = centro;

        if (Physics.Raycast(centro, Vector3.down, out RaycastHit chao, GroundProbe, ~0, QueryTriggerInteraction.Ignore))
            center.y = chao.point.y;

        center.y += groundOffset;
        radius = raio;
        shownAt = Time.time;
        line.enabled = true;
        Draw(startRadius, 1f);
    }

    private void Update()
    {
        if (!line.enabled)
            return;

        float t = Time.time - shownAt;

        if (t >= growTime + fadeTime)
        {
            line.enabled = false;
            return;
        }

        // Cresce rapido e freia no fim, como onda de impacto; depois some
        // ja parado no raio, que e o numero que importa ler.
        float cresceu = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / growTime), 3f);
        float alfa = t < growTime ? 1f : 1f - (t - growTime) / fadeTime;

        Draw(Mathf.Lerp(startRadius, radius, cresceu), alfa);
    }

    private void Draw(float raio, float alfa)
    {
        for (int i = 0; i < segments; i++)
            points[i] = center + circle[i] * raio;

        line.SetPositions(points);

        Color inicio = startColor;
        Color fim = endColor;
        inicio.a *= alfa;
        fim.a *= alfa;
        line.startColor = inicio;
        line.endColor = fim;
    }
}
