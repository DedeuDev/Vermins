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
/// Fica em LateUpdate de proposito: se seguisse no Update, a camera
/// poderia rodar antes do jogador ter se movido no frame, e a
/// imagem treme.
/// </summary>
[RequireComponent(typeof(Camera))]
public class IsometricCameraFollow : MonoBehaviour
{
    [Header("Alvo")]
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

    [Header("Suavizacao")]
    [Tooltip("Zero gruda a camera no alvo. Valores maiores deixam o " +
             "movimento mais macio, mas com mais atraso.")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Tremor")]
    [Tooltip("Quanto a camera anda, em metros, com o tremor no maximo. " +
             "Quem pede tremor manda um 'trauma' de 0 a 1 e o " +
             "deslocamento e trauma ao quadrado vezes isto: tremor fraco " +
             "fica bem fraco e so pancada forte sacode de verdade.")]
    [SerializeField] private float tremorMaximo = 0.7f;

    [Tooltip("Rapidez da sacudida. Alto parece impacto, baixo parece " +
             "terremoto.")]
    [SerializeField] private float frequenciaDoTremor = 25f;

    [Tooltip("Quanto trauma some por segundo. Com 2, o maior tremor " +
             "acaba em meio segundo.")]
    [SerializeField] private float recuperacaoDoTremor = 2f;

    private Camera cam;
    private Vector3 followVelocity;

    // So uma camera segue o jogador por cena. Guardo ela aqui pra quem
    // quiser tremer nao precisar achar a camera.
    private static IsometricCameraFollow ativa;

    // A posicao que o seguir calcula, sem o tremor. O tremor entra por
    // cima dela: se ele entrasse no transform.position, o SmoothDamp do
    // proximo frame partiria do ponto tremido e a camera sairia do
    // lugar.
    private Vector3 posicaoSeguida;
    private float trauma;

    /// <summary>Pra onde a camera olha. Calculada uma vez e nunca mais.</summary>
    private Quaternion Rotacao => Quaternion.Euler(pitch, yaw, 0f);

    /// <summary>
    /// Altura em metros que cabe na tela na distancia do alvo. Serve pra
    /// saber o tamanho do personagem na tela sem ter que abrir o jogo:
    /// um personagem de 1,9 m ocupa 1,9 dividido por isso.
    /// </summary>
    public float AlturaVisivel => 2f * distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>
    /// Sacode a camera. O trauma soma com o que ja tinha, ate 1: dois
    /// golpes seguidos tremem mais que um.
    /// </summary>
    public static void Tremer(float quanto)
    {
        if (ativa != null)
            ativa.trauma = Mathf.Clamp01(ativa.trauma + quanto);
    }

    private void OnEnable()
    {
        ativa = this;
    }

    private void OnDisable()
    {
        if (ativa == this)
            ativa = null;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        Aplicar();

        // Sem isso a camera entra na cena vinda de onde parou no editor
        // e passa o primeiro segundo voando ate o jogador.
        if (target != null)
            transform.position = PosicaoDesejada();

        posicaoSeguida = transform.position;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        posicaoSeguida = Vector3.SmoothDamp(
            posicaoSeguida,
            PosicaoDesejada(),
            ref followVelocity,
            smoothTime
        );

        transform.position = posicaoSeguida + Tremor();
    }

    /// <summary>
    /// Deslocamento do tremor neste frame. So de lado e pra cima no
    /// plano da tela, nunca giro: a regra da camera nao girar vale pro
    /// tremor tambem. Uso ruido de Perlin em vez de Random pra a
    /// sacudida ser continua e nao pular de um canto pro outro.
    /// </summary>
    private Vector3 Tremor()
    {
        if (trauma <= 0f)
            return Vector3.zero;

        // Tempo sem escala: o tremor termina mesmo com o jogo pausado.
        trauma = Mathf.Max(0f, trauma - recuperacaoDoTremor * Time.unscaledDeltaTime);

        float forca = trauma * trauma * tremorMaximo;
        float t = Time.unscaledTime * frequenciaDoTremor;
        float x = Mathf.PerlinNoise(t, 0.5f) * 2f - 1f;
        float y = Mathf.PerlinNoise(0.5f, t) * 2f - 1f;

        return (transform.right * x + transform.up * y) * forca;
    }

    private Vector3 PosicaoDesejada()
    {
        // Ando pra tras a partir do ponto enquadrado, na direcao pra onde
        // a camera olha. Assim mexer no pitch nao muda a distancia, e
        // mexer na distancia nao muda o angulo - da pra achar o
        // enquadramento sem os dois brigarem.
        return target.position + targetOffset - Rotacao * Vector3.forward * distance;
    }

    /// <summary>
    /// Deixa mexer nos numeros com o jogo rodando e ver na hora.
    /// </summary>
    private void Aplicar()
    {
        transform.rotation = Rotacao;

        if (cam != null)
            cam.fieldOfView = fieldOfView;
    }

    private void OnValidate()
    {
        cam = GetComponent<Camera>();
        Aplicar();

        if (target != null && !Application.isPlaying)
            transform.position = PosicaoDesejada();
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
