using UnityEngine;

/// <summary>
/// Anel no chao que mostra a canalizacao do teleporte pra HUB (tecla T)
/// e o aviso vermelho quando o jogador esta em combate. Sem ele o Player
/// so ficava parado em Idle por 5 s e o unico retorno era uma barra no
/// debug HUD.
///
/// Leio so as propriedades publicas do ReturnToHub sem mexer nele. O
/// filho AnelDoTeleporte nasce no Awake e fica reaproveitado com os
/// pontos do circulo num array pronto, pra nao gerar lixo por frame.
/// </summary>
public class ChannelRing : MonoBehaviour
{
    [Tooltip("Material transparente que aceita cor de vertice (EfeitoLinha).")]
    [SerializeField] private Material material;

    [Tooltip("Cor do arco azul claro durante a canalizacao.")]
    [SerializeField] private Color channelColor = new Color(0.45f, 0.8f, 1f, 0.9f);

    [Tooltip("Cor do anel inteiro na recusa por combate.")]
    [SerializeField] private Color warningColor = new Color(0.95f, 0.2f, 0.15f, 1f);

    [Tooltip("Raio do anel em metros.")]
    [SerializeField, Min(0.1f)] private float radius = 0.9f;

    [Tooltip("Largura da linha em metros.")]
    [SerializeField, Min(0.01f)] private float thickness = 0.08f;

    [Tooltip("Segmentos do circulo completo.")]
    [SerializeField, Min(8)] private int segments = 64;

    [Tooltip("Layers do chao visivel. Mesma mascara do PlayerMotor e PlayerController.")]
    [SerializeField] private LayerMask groundMask = (1 << 7) | (1 << 8);

    [Tooltip("Folga acima do chao pra linha nao piscar dentro do piso.")]
    [SerializeField, Min(0f)] private float groundOffset = 0.03f;

    [Tooltip("Segundos pro anel vermelho sumir depois da recusa.")]
    [SerializeField, Min(0.01f)] private float warningFadeTime = 0.5f;

    // O pivo do Player fica na cintura (~0,92 m acima do chao), entao o
    // raio parte meio metro acima dele pra achar o piso com folga.
    private const float GroundProbe = 4f;
    private const float FallbackFootOffset = -0.92f;

    private ReturnToHub returnToHub;
    private LineRenderer line;
    private Vector3[] circle;

    private bool wasChanneling;
    private bool wasShowingWarning;
    private float warningStartTime = -1f;
    private int lastCount = -1;

    private void Awake()
    {
        returnToHub = GetComponent<ReturnToHub>();

        var objeto = new GameObject("AnelDoTeleporte");
        objeto.transform.SetParent(transform, false);
        objeto.transform.localPosition = new Vector3(0f, FallbackFootOffset + groundOffset, 0f);
        // Alinhamento TransformZ deitado no chao (como no MoveMarker e no
        // TargetHighlight); no plano XY local, +Y vira a frente do Player.
        objeto.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        line = objeto.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = false;
        line.alignment = LineAlignment.TransformZ;
        line.widthMultiplier = thickness;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;

        // Deixo os segments + 1 pontos prontos no Awake: angulo 0 fica na
        // frente do Player (+Y local -> +Z do Player) e o ultimo fecha nele.
        circle = new Vector3[segments + 1];

        for (int i = 0; i < segments; i++)
        {
            float angulo = i * Mathf.PI * 2f / segments;
            circle[i] = new Vector3(
                Mathf.Sin(angulo) * radius,
                Mathf.Cos(angulo) * radius,
                0f);
        }

        circle[segments] = circle[0];
    }

    private void OnDisable()
    {
        wasChanneling = false;
        wasShowingWarning = false;
        warningStartTime = -1f;
        lastCount = -1;

        if (line != null)
            line.enabled = false;
    }

    // Rodo no LateUpdate pra reagir no mesmo frame em que o ReturnToHub
    // ligou ou cancelou a canalizacao no Update.
    private void LateUpdate()
    {
        if (returnToHub == null)
            return;

        bool channeling = returnToHub.IsChanneling;
        bool warning = returnToHub.ShowCombatWarning;
        bool warningStarted = warning && !wasShowingWarning;
        wasShowingWarning = warning;

        if (!channeling && !wasChanneling && !warningStarted && warningStartTime < 0f)
            return;

        if (channeling)
        {
            if (!wasChanneling)
            {
                wasChanneling = true;
                warningStartTime = -1f;
                lastCount = -1;
                PlaceOnGround();
                SetColor(channelColor);
                line.enabled = true;
            }

            int count = Mathf.Clamp(
                Mathf.CeilToInt(returnToHub.ChannelProgress * segments) + 1,
                1,
                segments + 1);

            if (count != lastCount)
            {
                lastCount = count;
                line.positionCount = count;
                line.SetPositions(circle);
            }

            return;
        }

        if (wasChanneling)
        {
            wasChanneling = false;
            lastCount = -1;
            line.enabled = false;
        }

        if (warningStarted)
        {
            warningStartTime = Time.time;
            lastCount = -1;
            PlaceOnGround();
            line.positionCount = segments + 1;
            line.SetPositions(circle);
            SetColor(warningColor);
            line.enabled = true;
            return;
        }

        if (warningStartTime >= 0f)
        {
            float t = Time.time - warningStartTime;

            if (t >= warningFadeTime)
            {
                warningStartTime = -1f;
                line.enabled = false;
                return;
            }

            Color c = warningColor;
            c.a *= 1f - t / warningFadeTime;
            SetColor(c);
        }
    }

    private void PlaceOnGround()
    {
        float localY = FallbackFootOffset + groundOffset;

        if (Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                Vector3.down,
                out RaycastHit chao,
                GroundProbe,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            localY = chao.point.y - transform.position.y + groundOffset;
        }

        line.transform.localPosition = new Vector3(0f, localY, 0f);
    }

    private void SetColor(Color cor)
    {
        line.startColor = cor;
        line.endColor = cor;
    }
}
