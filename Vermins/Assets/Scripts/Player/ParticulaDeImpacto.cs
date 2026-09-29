using UnityEngine;

/// <summary>
/// Espirra sangue no ponto do golpe: quando a espada do jogador acerta um
/// inimigo, e quando o jogador apanha. O jato sai pro lado oposto de quem
/// bateu, que e o que faz parecer que o golpe empurrou.
///
/// E um ParticleSystem so, criado aqui e reaproveitado: a cada acerto eu
/// mudo ele de lugar e peco um punhado de particulas com Emit. Nao nasce
/// nem morre objeto por golpe. Ele fica fora do Player (em espaco de
/// mundo) pra as gotas que ja sairam nao andarem junto com o personagem.
/// </summary>
[RequireComponent(typeof(PlayerCombat))]
public class ParticulaDeImpacto : MonoBehaviour
{
    [Tooltip("Material de particula com textura redonda e cor de vertice.")]
    [SerializeField] private Material material;

    [SerializeField] private Color corEscura = new Color(0.6f, 0.03f, 0.03f, 1f);
    [SerializeField] private Color corClara = new Color(0.95f, 0.12f, 0.06f, 1f);

    [SerializeField] private int particulasNoAcerto = 14;

    [Tooltip("O golpe que mata e o final do combo espirram mais.")]
    [SerializeField] private int particulasNaMorte = 26;

    [SerializeField] private int particulasAoApanhar = 10;

    [Tooltip("Altura do peito, de onde o golpe parte e onde o jogador " +
             "leva o dano.")]
    [SerializeField] private float alturaDoPeito = 1.1f;

    private PlayerCombat combat;
    private Health health;
    private ParticleSystem sistema;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<Health>();
        sistema = CriarSistema();
    }

    private void OnEnable()
    {
        combat.OnGolpeAcertou += Acertou;

        if (health != null)
            health.OnDamaged += Apanhou;
    }

    private void OnDisable()
    {
        combat.OnGolpeAcertou -= Acertou;

        if (health != null)
            health.OnDamaged -= Apanhou;
    }

    private void OnDestroy()
    {
        if (sistema != null)
            Destroy(sistema.gameObject);
    }

    private void Acertou(Health alvo, float dano)
    {
        Vector3 origem = transform.position + Vector3.up * alturaDoPeito;
        Espirrar(PontoNoCorpo(alvo.gameObject, origem), origem,
            alvo.IsDead || combat.GolpeFinal ? particulasNaMorte : particulasNoAcerto);
    }

    private void Apanhou(float dano, GameObject quemBateu)
    {
        Vector3 peito = transform.position + Vector3.up * alturaDoPeito;

        // Sem saber quem bateu (veneno, queda), o sangue sai pra cima.
        Vector3 origem = quemBateu != null
            ? quemBateu.transform.position + Vector3.up * alturaDoPeito
            : peito + Vector3.down;

        Espirrar(PontoNoCorpo(gameObject, origem), origem, particulasAoApanhar);
    }

    /// <summary>
    /// Ponto da casca do corpo mais perto de quem bateu. Com isso o
    /// sangue sai da superficie do inimigo, e nao do meio dele.
    /// </summary>
    private Vector3 PontoNoCorpo(GameObject corpo, Vector3 origem)
    {
        foreach (var c in corpo.GetComponentsInChildren<Collider>())
        {
            // ClosestPoint so funciona em collider solido, e MeshCollider
            // concavo nao aceita.
            if (c.isTrigger || (c is MeshCollider m && !m.convex))
                continue;

            Vector3 ponto = c.ClosestPoint(origem);
            ponto.y = Mathf.Max(ponto.y, corpo.transform.position.y);
            return ponto;
        }

        return corpo.transform.position + Vector3.up * alturaDoPeito;
    }

    private void Espirrar(Vector3 ponto, Vector3 origem, int quantas)
    {
        Vector3 longe = ponto - origem;
        longe.y = 0f;

        if (longe.sqrMagnitude < 0.0001f)
            longe = Vector3.forward;

        longe.Normalize();

        // Mais pra cima do que pra longe. Na primeira versao o jato ia
        // quase reto pra longe de quem bateu e, saindo da casca virada pro
        // jogador, atravessava o corpo do inimigo e sumia atras dele: no
        // print quase nao aparecia nada. A camera olha de cima, entao o
        // que sobe e o que se ve.
        Vector3 direcao = longe * 0.6f + Vector3.up;

        // Nasce um palmo pra fora do corpo, do lado de quem bateu, pra a
        // primeira parte do voo ja estar na frente dele.
        Vector3 inicio = ponto - longe * 0.15f;

        sistema.transform.SetPositionAndRotation(
            inicio, Quaternion.LookRotation(direcao));
        sistema.Emit(quantas);
    }

    private ParticleSystem CriarSistema()
    {
        var objeto = new GameObject("ParticulaDeImpacto");
        var ps = objeto.AddComponent<ParticleSystem>();

        // Parar antes de mexer: o sistema nasce tocando e algumas
        // propriedades nao aceitam mudar com ele rodando.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(corEscura, corClara);
        main.gravityModifier = 1.5f;
        main.maxParticles = 200;

        var emissao = ps.emission;
        emissao.enabled = false;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 40f;
        forma.radius = 0.05f;

        // Encolhe ate sumir em vez de apagar de uma vez.
        var tamanho = ps.sizeOverLifetime;
        tamanho.enabled = true;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = objeto.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return ps;
    }
}
