using UnityEngine;

/// <summary>
/// Pega uma build e escreve os numeros dela nos componentes que fazem o
/// combate acontecer.
///
/// Este script nao guarda nada. Ele traduz: o AtributosDeBuild sabe as
/// contas, o Health sabe vida, o MeleeAttack sabe dano, o PlayerCombat
/// sabe cooldown e alcance. Ninguem passou a depender de atributo -
/// tudo continua funcionando com o componente removido, so que com os
/// valores que estiverem gravados na cena.
///
/// Foi assim de proposito. O Health e o MeleeAttack tambem sao dos
/// inimigos do Leonardo, e eu nao ia fazer o arquivo dele passar a
/// exigir um asset de build pra funcionar.
///
/// O bonus do nivel tambem passa por aqui, e nao pelo NivelDoPersonagem.
/// Build e nivel mexem nos mesmos dois numeros, entao quem escreve tem
/// que ser um so: com dois scripts escrevendo, o resultado dependeria de
/// qual chegou por ultimo.
///
/// O equipamento entra pelo mesmo motivo. A conta e (build + pontos do
/// equipamento) x bonus do nivel: o equipamento soma no ponto, antes do
/// nivel, entao um ponto de anel no nivel 10 vale o mesmo que um ponto
/// de build no nivel 10.
/// </summary>
[DisallowMultipleComponent]
public class AtributosDoPersonagem : MonoBehaviour
{
    [Tooltip("Qual build este personagem esta jogando. Trocar este asset " +
             "muda dano, cadencia, vida e alcance de uma vez. Vazio " +
             "significa 'usa o que estiver na cena' - nao e erro.")]
    [SerializeField] private AtributosDeBuild build;

    [Tooltip("Se a vida deve encher ao aplicar a build. Ligado pro comeco " +
             "da partida, que e quando o personagem tem que nascer inteiro. " +
             "Quem for aplicar buff no meio da luta desliga isto, senao " +
             "equipar e desequipar um anel vira cura infinita.")]
    [SerializeField] private bool encherAVidaAoAplicar = true;

    // Opcional: um inimigo que use este componente nao tem nivel, e ai o
    // bonus fica em 1.
    private NivelDoPersonagem nivel;

    // Opcional pelo mesmo motivo: sem ele, os pontos extras ficam em zero.
    private EquipamentoDoPersonagem equipamento;

    public AtributosDeBuild Build => build;

    private void Awake()
    {
        nivel = GetComponent<NivelDoPersonagem>();
        equipamento = GetComponent<EquipamentoDoPersonagem>();
    }

    private void OnEnable()
    {
        if (nivel != null)
            nivel.OnNivelMudou += AoMudarDeNivel;

        if (equipamento != null)
            equipamento.OnEquipamentoMudou += AoMudarDeEquipamento;
    }

    private void OnDisable()
    {
        if (nivel != null)
            nivel.OnNivelMudou -= AoMudarDeNivel;

        if (equipamento != null)
            equipamento.OnEquipamentoMudou -= AoMudarDeEquipamento;
    }

    /// <summary>
    /// Aplico no Start e nao no Awake. O Health zera a vida atual no
    /// Awake dele, e a ordem entre dois Awake e indefinida no Unity - se
    /// eu chegasse antes, ele sobrescrevia meu numero logo em seguida.
    /// Todo Awake termina antes do primeiro Start, entao aqui e seguro.
    ///
    /// O bonus do nivel vai junto, neste mesmo Start, e nao num Start do
    /// NivelDoPersonagem: a ordem entre dois Start tambem e indefinida, e
    /// o que chegasse por ultimo apagaria o outro.
    /// </summary>
    private void Start()
    {
        Aplicar();
    }

    [ContextMenu("Aplicar agora")]
    public void Aplicar()
    {
        Aplicar(encherAVidaAoAplicar);
    }

    /// <summary>
    /// Subir de nivel jogando enche a vida, como no Path of Exile. O XP
    /// que volta do save ou da passagem de cena nao enche: quem restaura
    /// devolve a vida que estava logo depois.
    /// </summary>
    private void AoMudarDeNivel(bool subiuJogando)
    {
        Aplicar(subiuJogando);
    }

    /// <summary>
    /// Trocar de equipamento nunca enche a vida: o teto muda e a vida
    /// atual fica onde estava. Se enchesse, tirar e por um anel no meio
    /// da luta viraria cura infinita.
    /// </summary>
    private void AoMudarDeEquipamento()
    {
        Aplicar(false);
    }

    private void Aplicar(bool encherAVida)
    {
        if (build == null)
        {
            // Sem build eu nao tenho de que numero partir. Multiplicar o
            // que esta na cena faria o bonus compor a cada nivel, entao
            // prefiro avisar a aplicar errado.
            if (nivel != null)
            {
                Debug.LogWarning(
                    $"[{name}] Tem NivelDoPersonagem mas nenhuma build ligada, " +
                    "entao o bonus de nivel nao vale. Arrasta uma build no " +
                    "AtributosDoPersonagem.", this);
            }

            return;
        }

        var vida = GetComponent<Health>();
        var arma = GetComponent<MeleeAttack>();
        var combate = GetComponent<PlayerCombat>();

        // Sempre a partir dos numeros da build, nunca do que esta no
        // Health ou no MeleeAttack: ali ja tem o bonus do nivel anterior,
        // e multiplicar em cima faria os 10% por nivel virarem composto.
        // No nivel 10 seriam +136% em vez de +90%.
        float bonus = nivel != null ? nivel.Multiplicador : 1f;
        PontosExtras extra = equipamento != null ? equipamento.PontosSomados : default;

        if (vida != null)
            vida.DefinirVidaMaxima(build.VidaMaximaCom(extra.Vitalidade) * bonus, encherAVida);

        if (arma != null)
            arma.Damage = build.DanoCom(extra.Poder) * bonus;

        // O PlayerCombat so existe no jogador. Um inimigo pode usar este
        // mesmo componente pra vida e dano sem ter os outros dois.
        if (combate != null)
        {
            combate.AttackCooldown = build.CooldownCom(extra.Celeridade);
            combate.AttackRange = build.AlcanceEmMetros;
        }

        if (vida == null && arma == null && combate == null)
        {
            Debug.LogWarning(
                $"[{name}] Tem uma build ligada mas nenhum componente pra ela " +
                "mexer. Falta Health, MeleeAttack ou PlayerCombat neste objeto.",
                this);
        }
    }
}
