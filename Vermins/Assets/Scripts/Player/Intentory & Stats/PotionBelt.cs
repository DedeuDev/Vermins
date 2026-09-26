using UnityEngine;

/// <summary>
/// As pocoes que o jogador carrega, e o gole que cura.
///
/// A pocao nao cura quando o jogador encosta nela: ela vai pro cinto e
/// so cura quando ele bebe. E o que o GDR descreve - a §4.4 guarda pocao
/// no inventario e a §9.2 pede "acesso rapido a consumiveis". Curar ao
/// encostar ainda jogaria fora a pocao pega com a vida cheia.
///
/// A cura e uma fracao da vida maxima, e nao um numero fixo. A vida
/// maxima vem da build e vai de 70 (Franco Atirador) a 150 (Acolito).
/// Com 40 fixos, a pocao devolveria 57% da barra de um e 27% da do
/// outro, e encolheria a diferenca que a vitalidade compra no orcamento
/// da build. Com 40% da maxima, toda build recupera o mesmo pedaco.
/// </summary>
[RequireComponent(typeof(Health))]
public class PotionBelt : MonoBehaviour
{
    [Tooltip("Quantas pocoes cabem. Com o cinto cheio, a pocao do chao " +
             "fica onde esta. Valor de teste, nao e definitivo.")]
    [SerializeField, Min(1)] private int capacity = 5;

    [Tooltip("Fracao da vida maxima que cada pocao devolve. Valor de " +
             "teste, nao e definitivo.")]
    [SerializeField, Range(0.05f, 1f)] private float healFraction = 0.4f;

    [Tooltip("Com quantas o jogador comeca. Quando a loja do hub existir, " +
             "e ela que enche o cinto antes da dungeon.")]
    [SerializeField, Min(0)] private int startingCount = 0;

    public int Count { get; private set; }
    public int Capacity => capacity;
    public bool IsFull => Count >= capacity;

    /// <summary>
    /// (quantas tem, quantas cabem). Dispara ao guardar e ao beber.
    ///
    /// Pra quem for fazer a UI: a mesma regra do Health.OnChanged - assina
    /// o evento E le Count/Capacity uma vez ao ligar, porque o evento so
    /// avisa quando muda.
    /// </summary>
    public event System.Action<int, int> OnChanged;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
        Count = Mathf.Min(startingCount, capacity);
    }

    /// <summary>
    /// Guarda uma pocao. Devolve false com o cinto cheio, e quem chamou
    /// decide o que fazer - a pocao do chao, por exemplo, continua la.
    /// </summary>
    public bool TryStore()
    {
        if (IsFull)
            return false;

        Count++;
        OnChanged?.Invoke(Count, capacity);
        return true;
    }

    /// <summary>
    /// Bebe uma pocao. Devolve false quando nao bebeu, pra quem chamou
    /// poder avisar o jogador depois (som de erro, icone piscando).
    ///
    /// Com a vida cheia eu nao deixo beber: com cinco no maximo e sem
    /// loja ainda, perder uma pocao por apertar a tecla sem querer e
    /// castigo demais. Morto tambem nao bebe - o Heal ja ignoraria a
    /// cura, mas a pocao sairia do cinto do mesmo jeito.
    /// </summary>
    public bool TryDrink()
    {
        if (Count <= 0 || health.IsDead || health.Current >= health.Max)
            return false;

        Count--;
        health.Heal(health.Max * healFraction);
        OnChanged?.Invoke(Count, capacity);
        return true;
    }
}
