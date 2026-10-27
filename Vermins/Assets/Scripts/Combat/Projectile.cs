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
public class Projectile : MonoBehaviour
{
    [Header("Voo")]
    [Tooltip("Metros por segundo. Com 18 a bola cruza os 9 m de alcance " +
             "em meio segundo, que e o tempo que da pra ver ela sair e " +
             "ainda assim nao parecer que o golpe emperrou. Bem mais " +
             "lento que isso e o inimigo consegue simplesmente andar pra " +
             "fora do caminho toda vez.")]
    [SerializeField] private float speed = 18f;

    [Tooltip("Raio da bola pro teste de colisao. Nao precisa bater com o " +
             "tamanho do desenho - este numero e o que decide se acertou, " +
             "e um pouco mais gordo que a arte perdoa a mira.")]
    [SerializeField] private float radius = 0.15f;

    [Tooltip("Depois de voar isto tudo sem bater em nada, some sozinho. " +
             "Deixei maior que o alcance do ataque pra bola nao evaporar " +
             "no ar bem na frente de um inimigo que recuou.")]
    [SerializeField] private float maxRange = 14f;

    [Tooltip("Contra o que testar colisao. Deixei tudo menos a Ignore " +
             "Raycast, porque hoje os personagens e metade do blockout " +
             "estao todos na Default e filtrar por layer nao separaria " +
             "nada. Quem distingue inimigo de parede aqui e a presenca de " +
             "um Health, nao a layer. No dia em que o projeto tiver layer " +
             "de personagem, e so apertar esta mascara.")]
    [SerializeField] private LayerMask hitMask = ~(1 << 2);

    [Tooltip("Atravessa quem tem Health: tira vida e segue em frente, e cada " +
             "um so leva uma vez. Parede (sem Health) ainda para. E a onda " +
             "de choque do guerreiro; a bola de magia fica desligada.")]
    [SerializeField] private bool pierce;

    [Header("Sumico")]
    [Tooltip("Solto o rastro em vez de destruir junto, pra ele apagar " +
             "sozinho no ar. Sem isso o trail some de estalo e a bola " +
             "parece ter sido deletada em vez de ter batido.")]
    [SerializeField] private TrailRenderer trail;

    private float damage;
    private GameObject owner;
    private Transform ownerRoot;
    private Vector3 direction;
    private float traveled;
    private bool launched;

    // Quem quer saber do acerto, com quanto a vida perdeu. A magia do
    // PlayerCombat lanca sem, e a do MagicBolt usa pra numero, sangue e XP.
    private System.Action<Health, float> onHit;

    // Quem a onda ja atravessou. So existe com pierce, e nasce uma vez por
    // projetil, no Launch. 8 sobra: a onda vai 10 m.
    private Health[] pierced;
    private int piercedCount;

    // Buffer unico pra todas as bolas. O SphereCastAll criava um array
    // novo por bola em todo frame. Estatico da certo porque o Update e
    // single-thread e cada bola le o resultado antes da proxima lancar.
    // 16 sobra: o trecho de um frame tem uns 30 cm, e ali nao cabem
    // tantos colliders.
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    /// <summary>
    /// Poe a bola pra voar. Chame logo depois de instanciar - antes
    /// disso ela fica parada de proposito, pra nao sair andando na
    /// direcao errada por um frame.
    /// </summary>
    public void Launch(Vector3 direction, float damage, GameObject owner)
    {
        Launch(direction, damage, owner, null);
    }

    /// <summary>
    /// Igual ao outro Launch, e no acerto avisa o onHit com o alvo e quanto
    /// a vida dele perdeu. Acerto em alvo invulneravel (perdeu zero) tambem
    /// avisa; quem escuta decide se conta.
    /// </summary>
    public void Launch(Vector3 direction, float damage, GameObject owner,
                       System.Action<Health, float> onHit)
    {
        this.onHit = onHit;

        if (pierce && pierced == null)
            pierced = new Health[8];

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

        // Com pierce, um mesmo trecho pode ter dois inimigos colados: testo
        // de novo ate nao sobrar ninguem novo. Cada volta pula quem ja levou,
        // entao o laco acaba.
        while (CastHit(passo, out RaycastHit hit))
        {
            if (!HandleImpact(hit))
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

        int total = Physics.SphereCastNonAlloc(
            transform.position, radius, direction, hitBuffer, passo,
            hitMask, QueryTriggerInteraction.Ignore);

        float maisPerto = float.MaxValue;
        bool achou = false;

        for (int i = 0; i < total; i++)
        {
            RaycastHit h = hitBuffer[i];

            // A propria bola e quem lancou nao contam.
            if (ownerRoot != null && h.collider.transform.IsChildOf(ownerRoot))
                continue;

            // A onda passa por quem ja levou e por quem esta morto: um corpo
            // no caminho nao pode segurar a onda.
            if (pierce)
            {
                Health vida = h.collider.GetComponentInParent<Health>();

                if (vida != null && (vida.IsDead || WasPierced(vida)))
                    continue;
            }

            if (h.distance < maisPerto)
            {
                maisPerto = h.distance;
                hit = h;
                achou = true;
            }
        }

        return achou;
    }

    /// <summary>
    /// Trata o que a bola encostou. Devolve true se ela segue voando (a
    /// onda atravessando alguem), false se parou e sumiu.
    /// </summary>
    private bool HandleImpact(RaycastHit hit)
    {
        // Quem tem Health leva dano; o resto e cenario e so para a bola.
        // Testo pelo componente e nao pela layer porque hoje inimigo e
        // parede dividem a Default - ver o tooltip da mascara.
        Health vida = hit.collider.GetComponentInParent<Health>();

        if (vida != null && !vida.IsDead)
        {
            // Meco o que a vida perdeu, como o LandHit: no alvo com 5 de
            // vida sai 5, e no invulneravel sai zero.
            float vidaAntes = vida.Current;
            vida.TakeDamage(damage, owner);
            onHit?.Invoke(vida, vidaAntes - vida.Current);

            // A onda marca e segue. Sem lugar na lista (mais de 8 no
            // caminho), ela para neste, pra nao acertar ninguem duas vezes.
            if (pierce && piercedCount < pierced.Length)
            {
                pierced[piercedCount++] = vida;
                return true;
            }
        }

        transform.position = hit.point;
        Despawn();
        return false;
    }

    private bool WasPierced(Health vida)
    {
        for (int i = 0; i < piercedCount; i++)
        {
            if (pierced[i] == vida)
                return true;
        }

        return false;
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
