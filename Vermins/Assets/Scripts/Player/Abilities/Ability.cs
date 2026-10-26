using UnityEngine;

/// <summary>
/// Base de toda habilidade do Player: golpe em area, projetil, o que vier.
/// Quem herda so diz o que a habilidade faz, no Activate. Recarga, a trava
/// de morto e rolando e o aviso de uso ficam aqui.
///
/// Fiz no molde do Pickup: cada habilidade nova herda e ja ganha tudo isso,
/// e quem precisa saber delas (o HUD da recarga, a 3.5) conversa com a base
/// sem listar uma por uma.
///
/// Quem manda usar e o PlayerController, como no Dodge. A habilidade nao le
/// input: recebe o ponto do chao onde o mouse estava e decide o resto. Uma
/// quest ou um tutorial poderia chamar o TryUse no lugar do botao.
///
/// A recarga e so um horario guardado, comparado com o Time.time. Sem
/// Update, entao habilidade parada nao custa nada por frame.
/// </summary>
[RequireComponent(typeof(Health))]
public abstract class Ability : MonoBehaviour
{
    [Tooltip("Segundos entre um uso e o proximo, contados do uso. A " +
             "Celeridade vai encurtar isto na 3.4.")]
    [SerializeField, Min(0f)] protected float cooldown = 3f;

    private Health health;
    private Dodge dodge;

    // Quando a habilidade volta. Guardo o horario, e nao o tempo que falta,
    // pra nao precisar descontar nada por frame.
    private float readyTime;

    // Quando o gesto termina e o jogador volta a mandar. Mesmo esquema da
    // recarga: um horario, sem Update.
    private float castEndTime;

    /// <summary>
    /// Disparado depois de cada uso que deu certo. Animacao, efeito e o HUD
    /// escutam daqui, sem a filha precisar chamar nenhum deles.
    /// </summary>
    public event System.Action<Ability> OnUsed;

    /// <summary>
    /// A recarga que vale agora. Virtual porque a 3.4 vai tirar a Celeridade
    /// daqui, e quem le (o HUD, o TryUse) nao precisa saber disso.
    /// </summary>
    public virtual float Cooldown => cooldown;

    public bool IsReady => Time.time >= readyTime;

    /// <summary>Segundos ate poder usar de novo. 0 quando pronta.</summary>
    public float CooldownRemaining => Mathf.Max(0f, readyTime - Time.time);

    /// <summary>
    /// Quanto da recarga falta, de 1 (acabou de usar) a 0 (pronta). E o
    /// numero que a barra do HUD desenha.
    /// </summary>
    public float CooldownFraction => Cooldown > 0f ? Mathf.Clamp01(CooldownRemaining / Cooldown) : 0f;

    /// <summary>
    /// Quanto o gesto trava o jogador, do aperto ate poder andar de novo.
    /// Zero e habilidade instantanea. O PlayerAnimator encaixa o clipe neste
    /// tempo.
    /// </summary>
    public virtual float CastTime => 0f;

    /// <summary>
    /// Verdadeiro durante o gesto. O PlayerController trata igual ao
    /// rolamento: guarda o clique e usa no fim, pra o corpo nao sair
    /// deslizando no meio da pancada.
    /// </summary>
    public bool IsCasting => Time.time < castEndTime;

    protected virtual void Awake()
    {
        health = GetComponent<Health>();
        dodge = GetComponent<Dodge>();
    }

    /// <summary>
    /// Usa a habilidade na direcao do ponto, que e onde o mouse esta no
    /// chao. Devolve false na recarga, morto, rolando ou se a filha recusar,
    /// e ai a recarga nao comeca.
    /// </summary>
    public bool TryUse(Vector3 point)
    {
        // Rolando nao: o rolamento ja corta golpe e e invulneravel, e soltar
        // habilidade no meio dele deixaria rolar e bater ao mesmo tempo. O
        // PlayerController guarda o comando e tenta de novo no fim.
        if (!IsReady || health.IsDead || (dodge != null && dodge.IsDodging))
            return false;

        if (!Activate(point))
            return false;

        // Conto a recarga depois do Activate pra uma recusa da filha (sem
        // alvo, sem chao) nao gastar o uso.
        readyTime = Time.time + Cooldown;
        castEndTime = Time.time + CastTime;
        OnUsed?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Solta o jogador antes do fim do gesto. A filha chama quando o gesto
    /// e cortado (a esquiva no meio da pancada). A recarga continua: cortar
    /// nao devolve o uso.
    /// </summary>
    protected void EndCast()
    {
        castEndTime = 0f;
    }

    /// <summary>
    /// O que a habilidade faz. Devolve false se nao deu pra usar agora; a
    /// base ja conferiu recarga, morte e rolamento antes de chamar.
    /// </summary>
    protected abstract bool Activate(Vector3 point);
}
