using UnityEngine;

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
public class MarcadorDeDestino : MonoBehaviour
{
    [Tooltip("Material transparente que aceita cor de vertice (URP " +
             "Particles/Unlit). E a cor do LineRenderer que da o fade.")]
    [SerializeField] private Material material;

    [SerializeField] private Color cor = new Color(1f, 0.85f, 0.55f, 0.9f);

    [Tooltip("Raio do anel no clique. Ele fecha ate o raioFinal.")]
    [SerializeField] private float raioInicial = 0.55f;

    [SerializeField] private float raioFinal = 0.2f;

    [Tooltip("Quanto tempo o anel fica na tela.")]
    [SerializeField] private float duracao = 0.4f;

    [SerializeField] private float espessura = 0.05f;

    [SerializeField] private int segmentos = 40;

    [Tooltip("Folga acima do chao pra o anel nao piscar dentro dele.")]
    [SerializeField] private float alturaAcimaDoChao = 0.03f;

    private LineRenderer anel;

    // Hora em que o anel apareceu. Negativo quando ele esta escondido.
    private float inicio = -1f;

    // Frame do ultimo OnMoveOrdered. Evento no frame seguinte ao anterior
    // quer dizer botao segurado, nao clique novo.
    private int ultimoFrameDaOrdem = -10;

    private void Awake()
    {
        var objeto = new GameObject("MarcadorDeDestino");

        // Deitado no chao: com o alinhamento TransformZ a linha fica de
        // frente pro Z local, e girando 90 no X esse Z aponta pra cima.
        objeto.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        anel = objeto.AddComponent<LineRenderer>();
        anel.sharedMaterial = material;
        anel.useWorldSpace = false;
        anel.loop = true;
        anel.alignment = LineAlignment.TransformZ;
        anel.positionCount = segmentos;
        anel.widthMultiplier = espessura;
        anel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        anel.receiveShadows = false;
        anel.enabled = false;
    }

    private void OnEnable()
    {
        PlayerController.OnMoveOrdered += Mostrar;
    }

    private void OnDisable()
    {
        PlayerController.OnMoveOrdered -= Mostrar;
        Esconder();
    }

    private void OnDestroy()
    {
        if (anel != null)
            Destroy(anel.gameObject);
    }

    private void Mostrar(Vector3 ponto)
    {
        bool segurando = Time.frameCount - ultimoFrameDaOrdem <= 1;
        ultimoFrameDaOrdem = Time.frameCount;

        if (segurando)
            return;

        anel.transform.position = ponto + Vector3.up * alturaAcimaDoChao;
        inicio = Time.time;
        Desenhar(0f);
        anel.enabled = true;
    }

    private void Update()
    {
        if (inicio < 0f)
            return;

        float t = (Time.time - inicio) / duracao;

        if (t >= 1f)
        {
            Esconder();
            return;
        }

        Desenhar(t);
    }

    private void Desenhar(float t)
    {
        // Fecha rapido no comeco e assenta no fim (ease-out), e o fade
        // vai no sentido contrario: quase inteiro no comeco e cai no
        // fim. Assim o anel e bem visivel no instante do clique.
        float fechamento = 1f - (1f - t) * (1f - t);
        float raio = Mathf.Lerp(raioInicial, raioFinal, fechamento);

        for (int i = 0; i < segmentos; i++)
        {
            float angulo = i * Mathf.PI * 2f / segmentos;
            anel.SetPosition(i, new Vector3(
                Mathf.Cos(angulo) * raio,
                Mathf.Sin(angulo) * raio,
                0f));
        }

        Color c = cor;
        c.a *= 1f - t * t;
        anel.startColor = c;
        anel.endColor = c;
    }

    private void Esconder()
    {
        inicio = -1f;

        if (anel != null)
            anel.enabled = false;
    }
}
