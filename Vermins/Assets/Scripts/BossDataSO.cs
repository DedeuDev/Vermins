using UnityEngine;

[CreateAssetMenu(fileName = "NovoBossLadinoDados", menuName = "IA/Dados de Boss Ladino")]
public class BossDataSO : ScriptableObject
{
    [Header("Identificação")]
    public string nomeBoss = "Ladino Sombrio";
    public float vidaMaxima = 1000f;

    [Header("Movimentação")]
    public float velocidadeNativo = 4.5f;
    public float velocidadeFurtivo = 6.5f;
    public float distanciaSegura = 7.0f;

    [Header("Ataque Ranged (Besta)")]
    public float alcanceBesta = 10.0f;
    public float cooldownFlechaFase1 = 1.8f;
    public float cooldownFlechaFase2 = 1.0f;
    public GameObject prefabFlecha;
    public Transform pontoDisparo;

    [Header("Habilidade: Bomba de Veneno")]
    public float cooldownVeneno = 8.0f;
    public GameObject prefabPocaVeneno;

    [Header("Habilidade: Furtividade / Invisibilidade")]
    public float tempoInvisivel = 2.5f;
    public float cooldownInvisibilidadeFase2 = 12.0f;
    public GameObject prefabEfeitoFumaca;
}