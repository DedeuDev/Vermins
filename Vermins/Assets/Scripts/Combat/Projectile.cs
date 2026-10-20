using UnityEngine;
using UnityEngine.Serialization;

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
public class Projectile : MonoBehaviour
{
    [Header("Voo")]
    [Tooltip("Metros por segundo. Com 18 a bola cruza os 9 m de alcance " +
             "em meio segundo, que e o tempo que da pra ver ela sair e " +
             "ainda assim nao parecer que o golpe emperrou. Bem mais " +
             "lento que isso e o inimigo consegue simplesmente andar pra " +
             "fora do caminho toda vez.")]
    [FormerlySerializedAs("velocidade")]
    [SerializeField] private float speed = 18f;

    [Tooltip("Raio da bola pro teste de colisao. Nao precisa bater com o " +
             "tamanho do desenho - este numero e o que decide se acertou, " +
             "e um pouco mais gordo que a arte perdoa a mira.")]
    [FormerlySerializedAs("raio")]
    [SerializeField] private float radius = 0.15f;

    [Tooltip("Depois de voar isto tudo sem bater em nada, some sozinho. " +
             "Deixei maior que o alcance do ataque pra bola nao evaporar " +
             "no ar bem na frente de um inimigo que recuou.")]
    [FormerlySerializedAs("alcanceMaximo")]
    [SerializeField] private float maxRange = 14f;

    [Tooltip("Contra o que testar colisao. Deixei tudo menos a Ignore " +
             "Raycast, porque hoje os personagens e metade do blockout " +
             "estao todos na Default e filtrar por layer nao separaria " +
             "nada. Quem distingue inimigo de parede aqui e a presenca de " +
             "um Health, nao a layer. No dia em que o projeto tiver layer " +
             "de personagem, e so apertar esta mascara.")]
    [FormerlySerializedAs("oQueAcerta")]
    [SerializeField] private LayerMask hitMask = ~(1 << 2);

    [Header("Sumico")]
    [Tooltip("Solto o rastro em vez de destruir junto, pra ele apagar " +
             "sozinho no ar. Sem isso o trail some de estalo e a bola " +
             "parece ter sido deletada em vez de ter batido.")]
    [FormerlySerializedAs("rastro")]
    [SerializeField] private TrailRenderer trail;

    private float damage;
    private GameObject owner;
    private Transform ownerRoot;
    private Vector3 direction;
    private float traveled;
    private bool launched;

    /// <summary>
    /// Poe a bola pra voar. Chame logo depois de instanciar - antes
    /// disso ela fica parada de proposito, pra nao sair andando na
    /// direcao errada por um frame.
    /// </summary>
    public void Launch(Vector3 direction, float damage, GameObject owner)
    {
        // Aqui eu achatava o Y, e isso so nao doia porque o blockout era
        // plano. Com o alvo em cima de uma plataforma de 2,5 m a bola
        // saia da mao reta e passava por baixo do bicho: o jogador subia
        // a rampa, entrava em alcance, fazia o gesto e nao acertava
        // nada. Medi 14 s de ataque contra o inimigo da plataforma e deu
        // zero de dano.
        //
        // Agora ela sobe e desce. Quem mira e o PlayerCombat, que aponta
        // pro transform do alvo - e esse fica na altura do peito, nao no
        // pe, entao no chao plano o tiro sai praticamente reto do mesmo
        // jeito que saia antes.

        // Direcao zerada acontece quando o alvo esta exatamente em cima
        // de quem lancou. Sem esta linha o normalized devolve zero e a
        // bola fica parada no ar pra sempre.
        this.direction = direction.sqrMagnitude < 0.0001f
            ? transform.forward
            : direction.normalized;

        this.damage = damage;
        this.owner = owner;

        // Guardo a raiz e nao o GameObject porque o collider de quem
        // lancou costuma estar na raiz e a bola nasce dentro dele. Sem
        // ignorar isto, o primeiro frame ja acerta o proprio dono.
        ownerRoot = owner != null ? owner.transform.root : null;

        transform.forward = this.direction;
        launched = true;
    }

    private void Update()
    {
        if (!launched)
            return;

        float passo = speed * Time.deltaTime;

        if (CastHit(passo, out RaycastHit hit))
        {
            HandleImpact(hit);
            return;
        }

        transform.position += direction * passo;
        traveled += passo;

        if (traveled >= maxRange)
            Despawn();
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
    private bool CastHit(float passo, out RaycastHit hit)
    {
        hit = default;

        RaycastHit[] tudo = Physics.SphereCastAll(
            transform.position, radius, direction, passo,
            hitMask, QueryTriggerInteraction.Ignore);

        float maisPerto = float.MaxValue;
        bool achou = false;

        foreach (RaycastHit h in tudo)
        {
            // A propria bola e quem lancou nao contam.
            if (ownerRoot != null && h.collider.transform.IsChildOf(ownerRoot))
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

    private void HandleImpact(RaycastHit hit)
    {
        transform.position = hit.point;

        // Quem tem Health leva dano; o resto e cenario e so para a bola.
        // Testo pelo componente e nao pela layer porque hoje inimigo e
        // parede dividem a Default - ver o tooltip da mascara.
        Health vida = hit.collider.GetComponentInParent<Health>();

        if (vida != null && !vida.IsDead)
            vida.TakeDamage(damage, owner);

        Despawn();
    }

    private void Despawn()
    {
        if (trail != null)
        {
            trail.transform.SetParent(null, true);
            trail.autodestruct = true;
            trail.emitting = false;
        }

        Destroy(gameObject);
    }
}
