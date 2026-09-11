using UnityEngine;

[CreateAssetMenu(fileName = "NovoBossDados", menuName = "IA/Dados de Boss")]
public class BossDataSO : ScriptableObject
{
    [Header("Identificação")]
    public string nomeBoss = "Rato Mago Transmorfo";
    public float vidaMaxima = 1000f;

    [Header("Movimentação")]
    public float velocidadeMago = 3.5f;
    public float velocidadeBesta = 6.0f;
    public float distanciaSeguraMago = 6.0f; // Distância que o Mago tenta manter do player

    [Header("Ataques Arcanos (Forma Mago)")]
    public float alcanceProjetil = 12.0f;
    public float cooldownProjetil = 2.0f;
    public GameObject prefabProjetil;
    public Transform pontoDisparo;

    [Header("Habilidade: Teleporte")]
    public float cooldownTeleporte = 8.0f;
    public float raioTeleporte = 5.0f;

    [Header("Habilidade: Invocação de Minions")]
    public float cooldownInvocacao = 15.0f;
    public GameObject prefabMinion;
    public int quantidadeMinions = 3;

    [Header("Ataques de Besta (Forma Transmorfa/Melee)")]
    public float alcanceAtaqueBesta = 2.0f;
    public float cooldownAtaqueBesta = 1.2f;
    public float danoAtaqueBesta = 25f;
}