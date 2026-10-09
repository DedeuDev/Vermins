using UnityEngine;

[CreateAssetMenu(fileName = "NovoBossMongeDados", menuName = "IA/Dados de Boss Monge")]
public class BossMongeDataSO : BossDataSO
{
    [Header("Movimentação")]
    public float velocidadeCaminhada = 4.0f;
    public float velocidadeAproximacao = 6.0f;

    [Header("Ataque Melee")]
    public float alcanceAtaqueMelee = 2.0f;
    public float cooldownAtaqueMelee = 1.2f;
    public float danoMelee = 15f;

    [Header("Habilidade: Terremoto (Impacto no Chão)")]
    public float alcanceDecisaoTerremoto = 4.0f;
    public float raioImpactoTerremoto = 3.5f;
    public float cooldownTerremoto = 7.0f;
    public float danoTerremoto = 25f; // <--- Adicionado aqui
    public GameObject prefabEfeitoTerremoto;

    [Header("Habilidade Fase 2: Escudo de Ki")]
    public float duracaoEscudoKi = 4.0f;
    public float cooldownEscudoKi = 12.0f;
    public GameObject prefabEscudoKi;
}