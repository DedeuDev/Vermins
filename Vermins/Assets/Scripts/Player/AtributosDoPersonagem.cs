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

    public AtributosDeBuild Build => build;

    /// <summary>
    /// Aplico no Start e nao no Awake. O Health zera a vida atual no
    /// Awake dele, e a ordem entre dois Awake e indefinida no Unity - se
    /// eu chegasse antes, ele sobrescrevia meu numero logo em seguida.
    /// Todo Awake termina antes do primeiro Start, entao aqui e seguro.
    /// </summary>
    private void Start()
    {
        Aplicar();
    }

    [ContextMenu("Aplicar agora")]
    public void Aplicar()
    {
        if (build == null)
            return;

        var vida = GetComponent<Health>();
        var arma = GetComponent<MeleeAttack>();
        var combate = GetComponent<PlayerCombat>();

        if (vida != null)
            vida.DefinirVidaMaxima(build.VidaMaxima, encherAVidaAoAplicar);

        if (arma != null)
            arma.Damage = build.Dano;

        // O PlayerCombat so existe no jogador. Um inimigo pode usar este
        // mesmo componente pra vida e dano sem ter os outros dois.
        if (combate != null)
        {
            combate.AttackCooldown = build.Cooldown;
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
