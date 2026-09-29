using UnityEngine;

/// <summary>
/// Mostra em quem o jogador vai bater e em quem ele ja esta batendo.
///
/// Inimigo embaixo do mouse ganha contorno vermelho: o shader
/// Vermins/Contorno entra como material extra nos renderers dele e sai
/// quando o mouse sai. O alvo travado no PlayerCombat ganha um anel
/// vermelho no pe, que pulsa e segue ele ate morrer ou o jogador
/// desistir.
///
/// O inimigo sob o mouse vem do CursorContextual, que ja faz os raios do
/// clique. Rodo no LateUpdate pra ler o que ele achou neste frame.
/// </summary>
[RequireComponent(typeof(CursorContextual))]
[RequireComponent(typeof(PlayerCombat))]
public class DestaqueDeAlvo : MonoBehaviour
{
    [Header("Contorno no inimigo sob o mouse")]
    [Tooltip("Material com o shader Vermins/Contorno.")]
    [SerializeField] private Material contorno;

    [Header("Anel no alvo travado")]
    [Tooltip("Material transparente com cor de vertice. Uso o mesmo do " +
             "marcador de destino.")]
    [SerializeField] private Material materialDoAnel;

    [SerializeField] private Color corDoAnel = new Color(0.9f, 0.15f, 0.1f, 0.9f);

    [SerializeField] private float espessuraDoAnel = 0.06f;

    [Tooltip("Quanto o anel passa da borda do corpo do alvo.")]
    [SerializeField] private float folgaDoRaio = 0.25f;

    [Tooltip("Quanto o raio cresce e encolhe no pulso, em fracao do raio.")]
    [SerializeField] private float pulso = 0.06f;

    [SerializeField] private float velocidadeDoPulso = 4f;

    [SerializeField] private int segmentos = 48;

    [Tooltip("Layers do chao visivel. Mesma do clique no PlayerController.")]
    [SerializeField] private LayerMask mascaraDoChao = (1 << 7) | (1 << 8);

    [Tooltip("Folga acima do chao pra o anel nao piscar dentro dele.")]
    [SerializeField] private float alturaAcimaDoChao = 0.03f;

    private CursorContextual cursor;
    private PlayerCombat combat;

    // Quem esta com contorno agora, e os materiais que cada renderer dele
    // tinha antes, pra devolver do jeito que estava.
    private Health contornado;
    private Renderer[] renderersContornados;
    private Material[][] materiaisOriginais;

    private LineRenderer anel;
    private Vector3[] pontos;
    private Health alvoDoAnel;
    private float raioDoAlvo;

    private void Awake()
    {
        cursor = GetComponent<CursorContextual>();
        combat = GetComponent<PlayerCombat>();

        // Mesma montagem do MarcadorDeDestino: fora do Player, deitado no
        // chao com o alinhamento TransformZ.
        var objeto = new GameObject("AnelDoAlvo");
        objeto.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        anel = objeto.AddComponent<LineRenderer>();
        anel.sharedMaterial = materialDoAnel;
        anel.useWorldSpace = false;
        anel.loop = true;
        anel.alignment = LineAlignment.TransformZ;
        anel.positionCount = segmentos;
        anel.widthMultiplier = espessuraDoAnel;
        anel.startColor = corDoAnel;
        anel.endColor = corDoAnel;
        anel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        anel.receiveShadows = false;
        anel.enabled = false;

        pontos = new Vector3[segmentos];
    }

    private void OnDisable()
    {
        // Sem isso, trocar de cena com o mouse num inimigo deixava o
        // contorno nele.
        TirarContorno();
        alvoDoAnel = null;

        if (anel != null)
            anel.enabled = false;
    }

    private void OnDestroy()
    {
        if (anel != null)
            Destroy(anel.gameObject);
    }

    private void LateUpdate()
    {
        AtualizarContorno(cursor.InimigoSobOMouse);
        AtualizarAnel(combat.HasTarget ? combat.Target : null);
    }

    private void AtualizarContorno(Health inimigo)
    {
        if (inimigo == contornado)
            return;

        TirarContorno();

        if (inimigo == null || contorno == null)
            return;

        contornado = inimigo;

        // So malha. LineRenderer e particula no inimigo nao tem normal
        // que preste pro contorno.
        var todos = inimigo.GetComponentsInChildren<Renderer>();
        int quantos = 0;

        foreach (var r in todos)
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
                quantos++;
        }

        renderersContornados = new Renderer[quantos];
        materiaisOriginais = new Material[quantos][];

        int i = 0;

        foreach (var r in todos)
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer))
                continue;

            Material[] originais = r.sharedMaterials;
            var comContorno = new Material[originais.Length + 1];
            originais.CopyTo(comContorno, 0);
            comContorno[originais.Length] = contorno;

            renderersContornados[i] = r;
            materiaisOriginais[i] = originais;
            r.sharedMaterials = comContorno;
            i++;
        }
    }

    private void TirarContorno()
    {
        if (renderersContornados != null)
        {
            for (int i = 0; i < renderersContornados.Length; i++)
            {
                // Se o inimigo foi destruido, o renderer ja foi junto.
                if (renderersContornados[i] != null)
                    renderersContornados[i].sharedMaterials = materiaisOriginais[i];
            }
        }

        contornado = null;
        renderersContornados = null;
        materiaisOriginais = null;
    }

    private void AtualizarAnel(Health alvo)
    {
        if (alvo == null)
        {
            alvoDoAnel = null;
            anel.enabled = false;
            return;
        }

        if (alvo != alvoDoAnel)
        {
            alvoDoAnel = alvo;
            raioDoAlvo = MedirRaio(alvo) + folgaDoRaio;
        }

        // O pivo do inimigo nao fica no pe (o agente dele tem baseOffset),
        // entao procuro o chao embaixo dele.
        Vector3 pe = alvo.transform.position;

        if (Physics.Raycast(
                pe + Vector3.up * 0.5f,
                Vector3.down,
                out RaycastHit hit,
                4f,
                mascaraDoChao,
                QueryTriggerInteraction.Ignore))
        {
            pe.y = hit.point.y;
        }

        anel.transform.position = pe + Vector3.up * alturaAcimaDoChao;

        float raio = raioDoAlvo *
            (1f + pulso * Mathf.Sin(Time.time * velocidadeDoPulso));

        for (int i = 0; i < segmentos; i++)
        {
            float angulo = i * Mathf.PI * 2f / segmentos;
            pontos[i] = new Vector3(Mathf.Cos(angulo) * raio, Mathf.Sin(angulo) * raio, 0f);
        }

        anel.SetPositions(pontos);
        anel.enabled = true;
    }

    /// <summary>
    /// Raio do corpo do alvo no chao, pelos colliders solidos dele. Meio
    /// metro se ele nao tiver nenhum.
    /// </summary>
    private static float MedirRaio(Health alvo)
    {
        float raio = 0f;

        foreach (var c in alvo.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger)
                continue;

            Vector3 e = c.bounds.extents;
            raio = Mathf.Max(raio, e.x, e.z);
        }

        return raio > 0f ? raio : 0.5f;
    }
}
