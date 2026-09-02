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
             "perspectiva e e o que faz parecer isometrico.")]
    [SerializeField] private float distance = 29.5f;

    [Tooltip("Campo de visao vertical. Quanto menor, menos as coisas da " +
             "borda da tela aparecem tortas - e o que da a cara de ARPG. " +
             "60 (o padrao do Unity) e de jogo em primeira pessoa.")]
    [Range(10f, 80f)]
    [SerializeField] private float fieldOfView = 30f;

    [Header("Suavizacao")]
    [Tooltip("Zero gruda a camera no alvo. Valores maiores deixam o " +
             "movimento mais macio, mas com mais atraso.")]
    [SerializeField] private float smoothTime = 0.15f;

    private Camera cam;
    private Vector3 followVelocity;

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

    private void Awake()
    {
        cam = GetComponent<Camera>();
        Aplicar();

        // Sem isso a camera entra na cena vinda de onde parou no editor
        // e passa o primeiro segundo voando ate o jogador.
        if (target != null)
            transform.position = PosicaoDesejada();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            PosicaoDesejada(),
            ref followVelocity,
            smoothTime
        );
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
