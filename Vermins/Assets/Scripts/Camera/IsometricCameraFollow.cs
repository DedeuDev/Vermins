using UnityEngine;

/// <summary>
/// Camera em visao elevada seguindo o jogador, que e o angulo que o
/// GDR pede pra leitura de combate e exploracao.
///
/// A regra que manda aqui e: a camera NUNCA gira. Ela so desliza.
/// Diablo 4 e Path of Exile 2 fazem assim, e e o que segura o mundo
/// parado embaixo do jogador. A versao anterior usava LookAt todo
/// frame e por isso balancava: como a posicao vem suavizada, ela fica
/// atrasada em relacao ao jogador, a direcao camera->jogador muda, e o
/// LookAt girava o mundo junto. Medi 6,2 graus de giro numa corrida em
/// zigue-zague - da pra ver de longe.
///
/// Zoom na roda (18/11), como no D4 e no PoE2: a distancia anda entre
/// minDistance e maxDistance e o pitch acompanha, mais deitado de perto
/// e mais de cima de longe. Na distancia padrao o pitch e exatamente o
/// do campo pitch, entao quem nao mexe na roda ve o mesmo de antes. Girar
/// pelo zoom nao quebra a regra de cima: o giro so muda quando o jogador
/// pede, nunca por causa do seguir.
///
/// Fica em LateUpdate de proposito: se seguisse no Update, a camera
/// poderia rodar antes do jogador ter se movido no frame, e a
/// imagem treme.
/// </summary>
[RequireComponent(typeof(Camera))]
public class IsometricCameraFollow : MonoBehaviour
{
    [Header("Alvo")]
    [Tooltip("Vazio procura o objeto com a tag Player no Awake. O prefab " +
             "nao guarda referencia de cena, entao e assim que ele acha o " +
             "Player em toda cena.")]
    [SerializeField] private Transform target;

    [Tooltip("Ponto que a camera enquadra, em relacao ao alvo. Subir um " +
             "pouco tira o jogador do centro exato e sobra mais chao na " +
             "frente dele, que e pra onde ele esta indo.")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Enquadramento")]
    [Tooltip("Inclinacao. 90 e olhar reto de cima, 0 e olhar do chao.")]
    [Range(20f, 89f)]
    [SerializeField] private float pitch = 55f;

    [Tooltip("Giro em torno do alvo. 0 olha na direcao do +Z do mundo.")]
    [Range(-180f, 180f)]
    [SerializeField] private float yaw = 0f;

    [Tooltip("Distancia da camera ate o alvo.\n\n" +
             "Anda junto com o campo de visao: os dois decidem o tamanho " +
             "do personagem na tela. Longe + campo estreito achata a " +
             "perspectiva e e o que faz parecer isometrico.\n\n" +
             "24,3 poe o Paladino no mesmo tamanho de tela que o Vampire " +
             "tinha a 29,5. Medi os dois parados no Idle, com pitch 55 e " +
             "campo 30: 109 px de altura em 1080p no Vampire, 108 no " +
             "Paladino. A conta pela altura do corpo nao serve: de pe o " +
             "Paladino tem 94% da altura do Vampire, mas na tela ficava " +
             "com 83%, porque a 55 graus o fundo do corpo tambem vira " +
             "altura, e a capa do Vampire tem 1,42 m de fundo contra 0,69. " +
             "O preco e ver menos chao: a altura visivel na distancia do " +
             "alvo cai de 15,8 pra 13,0 m.")]
    [SerializeField] private float distance = 24.3f;

    [Tooltip("Campo de visao vertical. Quanto menor, menos as coisas da " +
             "borda da tela aparecem tortas - e o que da a cara de ARPG. " +
             "60 (o padrao do Unity) e de jogo em primeira pessoa.")]
    [Range(10f, 80f)]
    [SerializeField] private float fieldOfView = 30f;

    [Header("Zoom na roda")]
    [Tooltip("Desligado, a roda nao faz nada e a camera fica sempre na " +
             "distancia e no pitch de cima.")]
    [SerializeField] private bool zoomEnabled = true;

    [Tooltip("Mais perto que a roda deixa chegar. Com 15 e campo 30, cabem " +
             "8,0 m de altura na tela (13,0 no padrao): o personagem fica " +
             "1,6 vez maior.")]
    [SerializeField, Min(1f)] private float minDistance = 15f;

    [Tooltip("Mais longe que a roda deixa ir. Com 30 cabem 16,1 m de altura " +
             "na tela. Mais longe que isso as tochas e os inimigos da " +
             "borda comecam a aparecer antes da sala acender.")]
    [SerializeField, Min(1f)] private float maxDistance = 30f;

    [Tooltip("Pitch na distancia minima. Mais deitado mostra o personagem " +
             "de frente e mais do fundo da sala.")]
    [Range(20f, 89f)]
    [SerializeField] private float nearPitch = 45f;

    [Tooltip("Pitch na distancia maxima. Mais de cima pra ler a sala inteira.")]
    [Range(20f, 89f)]
    [SerializeField] private float farPitch = 60f;

    [Tooltip("Metros por clique da roda. Com 2,5 sao 4 cliques do padrao " +
             "ate o minimo e 3 ate o maximo.")]
    [SerializeField, Min(0.1f)] private float zoomStep = 2.5f;

    [Tooltip("Tempo pra chegar na distancia nova. Igual ao do seguir.")]
    [SerializeField, Min(0f)] private float zoomSmoothTime = 0.15f;

    [Header("Suavizacao")]
    [Tooltip("Zero gruda a camera no alvo. Valores maiores deixam o " +
             "movimento mais macio, mas com mais atraso.")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Tremor")]
    [Tooltip("Quanto a camera anda, em metros, com o tremor no maximo. " +
             "Quem pede tremor manda um 'trauma' de 0 a 1 e o " +
             "deslocamento e trauma ao quadrado vezes isto: tremor fraco " +
             "fica bem fraco e so pancada forte sacode de verdade.")]
    [SerializeField] private float maxShake = 0.7f;

    [Tooltip("Rapidez da sacudida. Alto parece impacto, baixo parece " +
             "terremoto.")]
    [SerializeField] private float shakeFrequency = 25f;

    [Tooltip("Quanto trauma some por segundo. Com 2, o maior tremor " +
             "acaba em meio segundo.")]
    [SerializeField] private float shakeRecovery = 2f;

    private Camera cam;
    private Vector3 followVelocity;

    // Distancia que a roda pediu e a que a camera esta agora, a caminho
    // dela. As duas comecam no campo distance.
    private float targetDistance;
    private float currentDistance;
    private float zoomVelocity;
    private float currentPitch;

    // So uma camera segue o jogador por cena. Guardo ela aqui pra quem
    // quiser tremer nao precisar achar a camera.
    private static IsometricCameraFollow active;

    // O ponto enquadrado que o seguir calcula. Antes eu suavizava a
    // posicao da camera; com distancia e pitch fixos da no mesmo, porque a
    // camera e esse ponto mais um vetor constante. Com zoom o vetor muda,
    // e suavizar a posicao somaria o atraso do seguir ao do zoom e tiraria
    // o jogador do centro enquanto aproxima. O tremor entra por cima no
    // final: se entrasse aqui, o SmoothDamp do proximo frame partiria do
    // ponto tremido e a camera sairia do lugar.
    private Vector3 followedFocus;
    private float trauma;

    /// <summary>Pra onde a camera olha. So muda quando o zoom muda.</summary>
    private Quaternion Rotation => Quaternion.Euler(currentPitch, yaw, 0f);

    /// <summary>
    /// Altura em metros que cabe na tela na distancia do alvo. Serve pra
    /// saber o tamanho do personagem na tela sem ter que abrir o jogo:
    /// um personagem de 1,9 m ocupa 1,9 dividido por isso.
    /// </summary>
    public float VisibleHeight => 2f * currentDistance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);

    /// <summary>
    /// Metros da camera ate o alvo agora, com o zoom. O DungeonTheme poe a
    /// luz do Player por aqui.
    /// </summary>
    public float Distance => currentDistance;

    /// <summary>Pitch de agora, com o zoom. O teste le daqui.</summary>
    public float CurrentPitch => currentPitch;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>
    /// Sacode a camera. O trauma soma com o que ja tinha, ate 1: dois
    /// golpes seguidos tremem mais que um.
    /// </summary>
    public static void Shake(float quanto)
    {
        if (active != null)
            active.trauma = Mathf.Clamp01(active.trauma + quanto);
    }

    /// <summary>
    /// Aproxima (positivo, roda pra frente) ou afasta (negativo). Conto
    /// so o sinal: dependendo da configuracao do Input System o clique
    /// da roda vale 1 ou 120, e assim um clique e sempre um passo.
    /// </summary>
    public static void Zoom(float scroll)
    {
        if (active == null || !active.zoomEnabled || scroll == 0f)
            return;

        active.targetDistance = Mathf.Clamp(
            active.targetDistance - Mathf.Sign(scroll) * active.zoomStep,
            active.minDistance,
            active.maxDistance
        );
    }

    private void OnEnable()
    {
        active = this;
    }

    private void OnDisable()
    {
        if (active == this)
            active = null;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ResetZoom();
        Apply();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                target = player.transform;
        }

        // Sem isso a camera entra na cena vinda de onde parou no editor
        // e passa o primeiro segundo voando ate o jogador.
        if (target != null)
            transform.position = DesiredPosition();

        followedFocus = target != null ? target.position + targetOffset : transform.position;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        UpdateZoom();

        followedFocus = Vector3.SmoothDamp(
            followedFocus,
            target.position + targetOffset,
            ref followVelocity,
            smoothTime
        );

        transform.position = followedFocus - Rotation * Vector3.forward * currentDistance + ShakeOffset();
    }

    /// <summary>
    /// Deslocamento do tremor neste frame. So de lado e pra cima no
    /// plano da tela, nunca giro: a regra da camera nao girar vale pro
    /// tremor tambem. Uso ruido de Perlin em vez de Random pra a
    /// sacudida ser continua e nao pular de um canto pro outro.
    /// </summary>
    private Vector3 ShakeOffset()
    {
        if (trauma <= 0f)
            return Vector3.zero;

        // Tempo sem escala: o tremor termina mesmo com o jogo pausado.
        trauma = Mathf.Max(0f, trauma - shakeRecovery * Time.unscaledDeltaTime);

        float forca = trauma * trauma * maxShake;
        float t = Time.unscaledTime * shakeFrequency;
        float x = Mathf.PerlinNoise(t, 0.5f) * 2f - 1f;
        float y = Mathf.PerlinNoise(0.5f, t) * 2f - 1f;

        return (transform.right * x + transform.up * y) * forca;
    }

    /// <summary>
    /// Leva a distancia ate a que a roda pediu e acerta o pitch junto.
    /// Tempo sem escala: se pausar no meio do zoom, ele termina mesmo
    /// assim, igual ao tremor.
    /// </summary>
    private void UpdateZoom()
    {
        if (currentDistance == targetDistance)
            return;

        currentDistance = Mathf.SmoothDamp(
            currentDistance,
            targetDistance,
            ref zoomVelocity,
            zoomSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );

        // O SmoothDamp so chega no alvo no limite. Um milimetro antes eu
        // encosto, pra parar de girar a camera e mexer na luz todo frame.
        if (Mathf.Abs(currentDistance - targetDistance) < 0.001f)
        {
            currentDistance = targetDistance;
            zoomVelocity = 0f;
        }

        currentPitch = PitchAt(currentDistance);
        transform.rotation = Rotation;
    }

    /// <summary>
    /// Pitch pra uma distancia. Sao duas retas que se encontram na
    /// distancia padrao, e nao uma so de ponta a ponta: assim o padrao
    /// cai exatamente no campo pitch, mesmo que ele nao esteja no meio
    /// entre nearPitch e farPitch.
    /// </summary>
    private float PitchAt(float d)
    {
        if (d <= distance)
            return Mathf.Lerp(nearPitch, pitch, Mathf.InverseLerp(minDistance, distance, d));

        return Mathf.Lerp(pitch, farPitch, Mathf.InverseLerp(distance, maxDistance, d));
    }

    /// <summary>Volta pra distancia e o pitch padrao, sem animar.</summary>
    private void ResetZoom()
    {
        targetDistance = distance;
        currentDistance = distance;
        zoomVelocity = 0f;
        currentPitch = pitch;
    }

    private Vector3 DesiredPosition()
    {
        // Ando pra tras a partir do ponto enquadrado, na direcao pra onde
        // a camera olha. Assim mexer no pitch nao muda a distancia, e
        // mexer na distancia nao muda o angulo - da pra achar o
        // enquadramento sem os dois brigarem.
        return target.position + targetOffset - Rotation * Vector3.forward * currentDistance;
    }

    /// <summary>
    /// Deixa mexer nos numeros com o jogo rodando e ver na hora.
    /// </summary>
    private void Apply()
    {
        transform.rotation = Rotation;

        if (cam != null)
            cam.fieldOfView = fieldOfView;
    }

    private void OnValidate()
    {
        // O padrao tem que estar dentro da faixa do zoom.
        minDistance = Mathf.Min(minDistance, distance);
        maxDistance = Mathf.Max(maxDistance, distance);

        cam = GetComponent<Camera>();

        // Mexendo no Inspector com o jogo rodando, volto pro padrao pra ver
        // o numero novo na hora, como antes do zoom.
        ResetZoom();
        Apply();

        if (target != null && !Application.isPlaying)
            transform.position = DesiredPosition();
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, target.position + targetOffset);
        Gizmos.DrawWireSphere(target.position + targetOffset, 0.3f);
    }
}
