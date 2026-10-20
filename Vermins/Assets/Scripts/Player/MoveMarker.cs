using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Mostra no chao onde o jogador mandou andar: um anel que fecha e some
/// em menos de meio segundo, como no Diablo 4.
///
/// Escuta o PlayerController.OnMoveOrdered, entao o controle nao sabe
/// que eu existo. O anel e um LineRenderer que eu crio aqui mesmo, fora
/// do Player: se ele fosse filho, andaria junto com o personagem em vez
/// de ficar parado no ponto clicado.
///
/// Segurando o botao, o OnMoveOrdered dispara todo frame. So mostro o
/// anel no clique, senao ele ficaria nascendo em baixo do cursor o
/// tempo todo enquanto a pessoa arrasta.
/// </summary>
public class MoveMarker : MonoBehaviour
{
    [Tooltip("Material transparente que aceita cor de vertice (URP " +
             "Particles/Unlit). E a cor do LineRenderer que da o fade.")]
    [SerializeField] private Material material;

    [FormerlySerializedAs("cor")]
    [SerializeField] private Color color = new Color(1f, 0.85f, 0.55f, 0.9f);

    [Tooltip("Raio do anel no clique. Ele fecha ate o endRadius.")]
    [FormerlySerializedAs("raioInicial")]
    [SerializeField] private float startRadius = 0.55f;

    [FormerlySerializedAs("raioFinal")]
    [SerializeField] private float endRadius = 0.2f;

    [Tooltip("Quanto tempo o anel fica na tela.")]
    [FormerlySerializedAs("duracao")]
    [SerializeField] private float duration = 0.4f;

    [FormerlySerializedAs("espessura")]
    [SerializeField] private float thickness = 0.05f;

    [FormerlySerializedAs("segmentos")]
    [SerializeField] private int segments = 40;

    [Tooltip("Folga acima do chao pra o anel nao piscar dentro dele.")]
    [FormerlySerializedAs("alturaAcimaDoChao")]
    [SerializeField] private float groundOffset = 0.03f;

    private LineRenderer ring;

    // Hora em que o anel apareceu. Negativo quando ele esta escondido.
    private float startTime = -1f;

    // Frame do ultimo OnMoveOrdered. Evento no frame seguinte ao anterior
    // quer dizer botao segurado, nao clique novo.
    private int lastOrderFrame = -10;

    private void Awake()
    {
        var objeto = new GameObject("MoveMarker");

        // Deitado no chao: com o alinhamento TransformZ a linha fica de
        // frente pro Z local, e girando 90 no X esse Z aponta pra cima.
        objeto.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        ring = objeto.AddComponent<LineRenderer>();
        ring.sharedMaterial = material;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.alignment = LineAlignment.TransformZ;
        ring.positionCount = segments;
        ring.widthMultiplier = thickness;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.enabled = false;
    }

    private void OnEnable()
    {
        PlayerController.OnMoveOrdered += Show;
    }

    private void OnDisable()
    {
        PlayerController.OnMoveOrdered -= Show;
        Hide();
    }

    private void OnDestroy()
    {
        if (ring != null)
            Destroy(ring.gameObject);
    }

    private void Show(Vector3 ponto)
    {
        bool segurando = Time.frameCount - lastOrderFrame <= 1;
        lastOrderFrame = Time.frameCount;

        if (segurando)
            return;

        ring.transform.position = ponto + Vector3.up * groundOffset;
        startTime = Time.time;
        Draw(0f);
        ring.enabled = true;
    }

    private void Update()
    {
        if (startTime < 0f)
            return;

        float t = (Time.time - startTime) / duration;

        if (t >= 1f)
        {
            Hide();
            return;
        }

        Draw(t);
    }

    private void Draw(float t)
    {
        // Fecha rapido no comeco e assenta no fim (ease-out), e o fade
        // vai no sentido contrario: quase inteiro no comeco e cai no
        // fim. Assim o anel e bem visivel no instante do clique.
        float fechamento = 1f - (1f - t) * (1f - t);
        float raio = Mathf.Lerp(startRadius, endRadius, fechamento);

        for (int i = 0; i < segments; i++)
        {
            float angulo = i * Mathf.PI * 2f / segments;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(angulo) * raio,
                Mathf.Sin(angulo) * raio,
                0f));
        }

        Color c = color;
        c.a *= 1f - t * t;
        ring.startColor = c;
        ring.endColor = c;
    }

    private void Hide()
    {
        startTime = -1f;

        if (ring != null)
            ring.enabled = false;
    }
}
