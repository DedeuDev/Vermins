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
/// O inimigo sob o mouse vem do ContextualCursor, que ja faz os raios do
/// clique. Rodo no LateUpdate pra ler o que ele achou neste frame.
/// </summary>
[RequireComponent(typeof(ContextualCursor))]
[RequireComponent(typeof(PlayerCombat))]
public class TargetHighlight : MonoBehaviour
{
    [Header("Contorno no inimigo sob o mouse")]
    [Tooltip("Material com o shader Vermins/Contorno.")]
    [SerializeField] private Material contorno;

    [Tooltip("Cor do contorno no item sob o mouse. Clara, pra nao " +
             "confundir com o vermelho do inimigo.")]
    [SerializeField] private Color corDoContornoDeItem = new Color(1f, 0.95f, 0.75f, 1f);

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

    private ContextualCursor cursor;
    private PlayerCombat combat;

    // Quem esta com contorno agora, e os materiais que cada renderer dele
    // tinha antes, pra devolver do jeito que estava.
    private Component outlined;

    // Copia do material do contorno com a cor do item. Criada aqui pra
    // nao precisar de outro asset nem de outro campo no prefab.
    private Material itemOutline;
    private Renderer[] outlinedRenderers;
    private Material[][] originalMaterials;

    private LineRenderer ring;
    private Vector3[] points;
    private Health ringTarget;
    private float targetRadius;

    private void Awake()
    {
        cursor = GetComponent<ContextualCursor>();
        combat = GetComponent<PlayerCombat>();

        if (contorno != null)
        {
            itemOutline = new Material(contorno);
            itemOutline.SetColor("_CorDoContorno", corDoContornoDeItem);
        }

        // Mesma montagem do MoveMarker: fora do Player, deitado no
        // chao com o alinhamento TransformZ.
        var objeto = new GameObject("AnelDoAlvo");
        objeto.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        ring = objeto.AddComponent<LineRenderer>();
        ring.sharedMaterial = materialDoAnel;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.alignment = LineAlignment.TransformZ;
        ring.positionCount = segmentos;
        ring.widthMultiplier = espessuraDoAnel;
        ring.startColor = corDoAnel;
        ring.endColor = corDoAnel;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.enabled = false;

        points = new Vector3[segmentos];
    }

    private void OnDisable()
    {
        // Sem isso, trocar de cena com o mouse num inimigo deixava o
        // contorno nele.
        ClearOutline();
        ringTarget = null;

        if (ring != null)
            ring.enabled = false;
    }

    private void OnDestroy()
    {
        if (ring != null)
            Destroy(ring.gameObject);

        if (itemOutline != null)
            Destroy(itemOutline);
    }

    private void LateUpdate()
    {
        // O cursor so acha item quando nao tem inimigo embaixo, entao no
        // maximo um dos dois vem preenchido.
        if (cursor.EnemyUnderMouse != null)
            UpdateOutline(cursor.EnemyUnderMouse, contorno);
        else
            UpdateOutline(cursor.PickupUnderMouse, itemOutline);
        UpdateRing(combat.HasTarget ? combat.Target : null);
    }

    private void UpdateOutline(Component alvo, Material material)
    {
        if (alvo == outlined)
            return;

        ClearOutline();

        if (alvo == null || material == null)
            return;

        outlined = alvo;

        // So malha. LineRenderer e particula no inimigo nao tem normal
        // que preste pro contorno, e o rotulo do item e texto.
        var todos = alvo.GetComponentsInChildren<Renderer>();
        int quantos = 0;

        foreach (var r in todos)
        {
            if (CanOutline(r))
                quantos++;
        }

        outlinedRenderers = new Renderer[quantos];
        originalMaterials = new Material[quantos][];

        int i = 0;

        foreach (var r in todos)
        {
            if (!CanOutline(r))
                continue;

            Material[] originais = r.sharedMaterials;
            var comContorno = new Material[originais.Length + 1];
            originais.CopyTo(comContorno, 0);
            comContorno[originais.Length] = material;

            outlinedRenderers[i] = r;
            originalMaterials[i] = originais;
            r.sharedMaterials = comContorno;
            i++;
        }
    }

    // O TextMeshPro desenha com MeshRenderer tambem; sem o teste o nome do
    // item ganhava contorno em volta de cada letra.
    private static bool CanOutline(Renderer r)
    {
        return (r is MeshRenderer || r is SkinnedMeshRenderer) &&
               r.GetComponent<TMPro.TMP_Text>() == null;
    }

    private void ClearOutline()
    {
        if (outlinedRenderers != null)
        {
            for (int i = 0; i < outlinedRenderers.Length; i++)
            {
                // Se o inimigo foi destruido, o renderer ja foi junto.
                if (outlinedRenderers[i] != null)
                    outlinedRenderers[i].sharedMaterials = originalMaterials[i];
            }
        }

        outlined = null;
        outlinedRenderers = null;
        originalMaterials = null;
    }

    private void UpdateRing(Health alvo)
    {
        if (alvo == null)
        {
            ringTarget = null;
            ring.enabled = false;
            return;
        }

        if (alvo != ringTarget)
        {
            ringTarget = alvo;
            targetRadius = MeasureRadius(alvo) + folgaDoRaio;
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

        ring.transform.position = pe + Vector3.up * alturaAcimaDoChao;

        float raio = targetRadius *
            (1f + pulso * Mathf.Sin(Time.time * velocidadeDoPulso));

        for (int i = 0; i < segmentos; i++)
        {
            float angulo = i * Mathf.PI * 2f / segmentos;
            points[i] = new Vector3(Mathf.Cos(angulo) * raio, Mathf.Sin(angulo) * raio, 0f);
        }

        ring.SetPositions(points);
        ring.enabled = true;
    }

    /// <summary>
    /// Raio do corpo do alvo no chao, pelos colliders solidos dele. Meio
    /// metro se ele nao tiver nenhum.
    /// </summary>
    private static float MeasureRadius(Health alvo)
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
