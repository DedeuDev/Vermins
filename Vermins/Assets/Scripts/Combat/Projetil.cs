using UnityEngine;

/// <summary>
/// Bola de magia que sai da mao, voa reto e machuca no que encostar.
///
/// Existe porque o ataque do jogador passou a valer 9 m e o dano
/// acontecia na hora, sem nada sair da mao: dava pra matar um bicho do
/// outro lado da sala e a unica pista na tela era ele piscar de
/// vermelho. Agora o dano viaja, e da pra ver de onde veio.
///
/// Ele NAO persegue. Sai na direcao em que o alvo estava no instante do
/// lancamento e segue reto; se o bicho andar pro lado, passa direto.
/// Isso e de proposito - e o que faz o alcance grande custar alguma
/// coisa. Quem decide se vale a pena atirar e o PlayerCombat, que so
/// deixa lancar com linha de visao limpa.
///
/// Serve pro inimigo tambem, no dia em que algum deles atirar: quem
/// lanca passa o dano e o dono, e nada aqui dentro sabe quem e jogador.
/// </summary>
[DisallowMultipleComponent]
public class Projetil : MonoBehaviour
{
    [Header("Voo")]
    [Tooltip("Metros por segundo. Com 18 a bola cruza os 9 m de alcance " +
             "em meio segundo, que e o tempo que da pra ver ela sair e " +
             "ainda assim nao parecer que o golpe emperrou. Bem mais " +
             "lento que isso e o inimigo consegue simplesmente andar pra " +
             "fora do caminho toda vez.")]
    [SerializeField] private float velocidade = 18f;

    [Tooltip("Raio da bola pro teste de colisao. Nao precisa bater com o " +
             "tamanho do desenho - este numero e o que decide se acertou, " +
             "e um pouco mais gordo que a arte perdoa a mira.")]
    [SerializeField] private float raio = 0.15f;

    [Tooltip("Depois de voar isto tudo sem bater em nada, some sozinho. " +
             "Deixei maior que o alcance do ataque pra bola nao evaporar " +
             "no ar bem na frente de um inimigo que recuou.")]
    [SerializeField] private float alcanceMaximo = 14f;

    [Tooltip("Contra o que testar colisao. Deixei tudo menos a Ignore " +
             "Raycast, porque hoje os personagens e metade do blockout " +
             "estao todos na Default e filtrar por layer nao separaria " +
             "nada. Quem distingue inimigo de parede aqui e a presenca de " +
             "um Health, nao a layer. No dia em que o projeto tiver layer " +
             "de personagem, e so apertar esta mascara.")]
    [SerializeField] private LayerMask oQueAcerta = ~(1 << 2);

    [Header("Sumico")]
    [Tooltip("Solto o rastro em vez de destruir junto, pra ele apagar " +
             "sozinho no ar. Sem isso o trail some de estalo e a bola " +
             "parece ter sido deletada em vez de ter batido.")]
    [SerializeField] private TrailRenderer rastro;

    private float dano;
    private GameObject dono;
    private Transform raizDoDono;
    private Vector3 direcao;
    private float percorrido;
    private bool lancado;

    /// <summary>
    /// Poe a bola pra voar. Chame logo depois de instanciar - antes
    /// disso ela fica parada de proposito, pra nao sair andando na
    /// direcao errada por um frame.
    /// </summary>
    public void Lancar(Vector3 direcao, float dano, GameObject dono)
    {
        direcao.y = 0f;

        // Direcao zerada acontece quando o alvo esta exatamente em cima
        // de quem lancou. Sem esta linha o normalized devolve zero e a
        // bola fica parada no ar pra sempre.
        this.direcao = direcao.sqrMagnitude < 0.0001f
            ? transform.forward
            : direcao.normalized;

        this.dano = dano;
        this.dono = dono;

        // Guardo a raiz e nao o GameObject porque o collider de quem
        // lancou costuma estar na raiz e a bola nasce dentro dele. Sem
        // ignorar isto, o primeiro frame ja acerta o proprio dono.
        raizDoDono = dono != null ? dono.transform.root : null;

        transform.forward = this.direcao;
        lancado = true;
    }

    private void Update()
    {
        if (!lancado)
            return;

        float passo = velocidade * Time.deltaTime;

        if (Acertou(passo, out RaycastHit hit))
        {
            Bater(hit);
            return;
        }

        transform.position += direcao * passo;
        percorrido += passo;

        if (percorrido >= alcanceMaximo)
            Sumir();
    }

    /// <summary>
    /// Testa o trecho que a bola vai percorrer NESTE frame, e nao o
    /// ponto onde ela esta.
    ///
    /// Uso SphereCast e nao um collider com Rigidbody de proposito. A
    /// 18 m/s a bola anda 30 cm por frame a 60 fps, e as paredes do
    /// blockout sao finas: com colisao normal ela atravessaria de vez em
    /// quando, e seria daqueles bugs que so aparecem quando o frame cai.
    /// Varrendo o trecho inteiro isso nao tem como acontecer.
    /// </summary>
    private bool Acertou(float passo, out RaycastHit hit)
    {
        hit = default;

        RaycastHit[] tudo = Physics.SphereCastAll(
            transform.position, raio, direcao, passo,
            oQueAcerta, QueryTriggerInteraction.Ignore);

        float maisPerto = float.MaxValue;
        bool achou = false;

        foreach (RaycastHit h in tudo)
        {
            // A propria bola e quem lancou nao contam.
            if (raizDoDono != null && h.collider.transform.IsChildOf(raizDoDono))
                continue;

            if (h.distance < maisPerto)
            {
                maisPerto = h.distance;
                hit = h;
                achou = true;
            }
        }

        return achou;
    }

    private void Bater(RaycastHit hit)
    {
        transform.position = hit.point;

        // Quem tem Health leva dano; o resto e cenario e so para a bola.
        // Testo pelo componente e nao pela layer porque hoje inimigo e
        // parede dividem a Default - ver o tooltip da mascara.
        Health vida = hit.collider.GetComponentInParent<Health>();

        if (vida != null && !vida.IsDead)
            vida.TakeDamage(dano, dono);

        Sumir();
    }

    private void Sumir()
    {
        if (rastro != null)
        {
            rastro.transform.SetParent(null, true);
            rastro.autodestruct = true;
            rastro.emitting = false;
        }

        Destroy(gameObject);
    }
}
