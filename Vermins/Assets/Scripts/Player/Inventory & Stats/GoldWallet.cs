using UnityEngine;

/// <summary>
/// O ouro que o jogador carrega.
///
/// Diferente do cinto de pocao, aqui nao tem limite: ouro no chao que o
/// jogador nao consegue pegar so irrita. Quem gasta e a HubShop
/// (secao 4.4 do GDR), pelo TryGastar.
/// </summary>
public class GoldWallet : MonoBehaviour
{
    [Tooltip("Com quanto o jogador comeca num jogo novo. O save nao mexe " +
             "aqui: ele chama o Restore depois que a cena abre.")]
    [SerializeField, Min(0)] private int startingAmount = 0;

    public int Amount { get; private set; }

    /// <summary>
    /// (ouro atual, quanto entrou). Dispara ao ganhar e ao gastar; no
    /// gasto o "quanto entrou" vem negativo.
    ///
    /// Pra quem for fazer a UI: a mesma regra do Health.OnChanged - assina
    /// o evento E le Amount uma vez ao ligar, porque o evento so avisa
    /// quando muda.
    /// </summary>
    public event System.Action<int, int> OnChanged;

    private void Awake()
    {
        Amount = startingAmount;
    }

    public void Add(int quantia)
    {
        if (quantia <= 0)
            return;

        Amount += quantia;
        OnChanged?.Invoke(Amount, quantia);
    }

    /// <summary>
    /// Tira o ouro se tiver o suficiente. Devolve false e nao mexe em nada
    /// quando falta: gastar pela metade deixaria o ouro negativo ou cobraria
    /// sem entregar. Quantia zero ou negativa tambem devolve false, pra
    /// ninguem usar isto como um Add disfarcado.
    /// </summary>
    public bool TryGastar(int quantia)
    {
        if (quantia <= 0 || quantia > Amount)
            return false;

        Amount -= quantia;
        OnChanged?.Invoke(Amount, -quantia);
        return true;
    }

    /// <summary>
    /// Troca o ouro pelo que veio do save. So o SaveSystem chama isto.
    ///
    /// Dispara o OnChanged com 0 no "quanto entrou": esse ouro nao foi
    /// pego agora, entao a UI nao deve mostrar "+X" como se tivesse sido.
    /// </summary>
    public void Restore(int quantia)
    {
        Amount = Mathf.Max(0, quantia);
        OnChanged?.Invoke(Amount, 0);
    }
}
