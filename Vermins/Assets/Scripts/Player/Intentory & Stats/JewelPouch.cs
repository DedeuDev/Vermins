using UnityEngine;

/// <summary>
/// As joias que o jogador carrega.
///
/// Joia nao e ouro com outro desenho: e loot pra vender. A secao 4.4 do
/// GDR diz que a loja do hub compra "loot nao utilizado", e isso e o que
/// transforma exploracao em preparacao. Entao a joia fica guardada aqui
/// com o valor dela, e so vira ouro quando a loja existir e comprar.
///
/// Guardo a quantidade e o valor somado, e nao a lista de joias, porque
/// hoje toda joia e igual e so muda o preco. Quando existir tipo de joia
/// (rubi, esmeralda), isto vira uma lista - e o save do Rogger muda junto.
/// </summary>
public class JewelPouch : MonoBehaviour
{
    public int Count { get; private set; }

    /// <summary>Quanto a loja pagaria por todas juntas.</summary>
    public int TotalValue { get; private set; }

    /// <summary>
    /// (quantas tem, valor somado). Dispara ao guardar.
    ///
    /// Pra quem for fazer a UI: a mesma regra do Health.OnChanged - assina
    /// o evento E le Count/TotalValue uma vez ao ligar.
    /// </summary>
    public event System.Action<int, int> OnChanged;

    public void Add(int valor)
    {
        if (valor <= 0)
            return;

        Count++;
        TotalValue += valor;
        OnChanged?.Invoke(Count, TotalValue);
    }
}
