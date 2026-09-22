using UnityEngine;

/// <summary>
/// O ouro que o jogador carrega.
///
/// Diferente do cinto de pocao, aqui nao tem limite: ouro no chao que o
/// jogador nao consegue pegar so irrita. Quem vai gastar e a loja do hub
/// (secao 4.4 do GDR), que ainda nao existe - por isso ainda nao tem
/// metodo de gastar. Quando ela chegar, ela cria o dela aqui.
/// </summary>
public class GoldWallet : MonoBehaviour
{
    [Tooltip("Com quanto o jogador comeca. Pra teste; o save do Rogger " +
             "e que vai preencher isso de verdade.")]
    [SerializeField, Min(0)] private int startingAmount = 0;

    public int Amount { get; private set; }

    /// <summary>
    /// (ouro atual, quanto entrou). Dispara ao ganhar ouro.
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
}
