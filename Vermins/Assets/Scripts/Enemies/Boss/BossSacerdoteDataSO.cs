using UnityEngine;

[CreateAssetMenu(fileName = "BossSacerdoteDataSO", menuName = "IA/Dados de Boss Sacerdote")]
public class BossSacerdoteDataSO : BossDataSO
{
    [Header("Movimentação")]
    public float velocidadeCaminhada = 3f;
    public float distanciaSegura = 6f;

    [Header("Habilidade: Armadilha de Fogo")]
    public float cooldownArmadilha = 5f;
    public float raioArmadilha = 2.5f;
    public float tempoAvisoArmadilha = 1.0f;
    public float danoArmadilha = 35f;
    public GameObject prefabPilarFogo;

    [Header("Habilidade: Zoltraak (Raio Mágico)")]
    public float cooldownZoltraak = 4f;
    public float danoZoltraak = 25f;
    public float tempoAvisoZoltraak = 0.6f;
    public GameObject prefabRaioZoltraak; // Opcional: efeito visual do raio

    [Header("Habilidade: Meteoro de Magia")]
    public float cooldownMeteoro = 7f;
    public float raioMeteoro = 4.5f;
    public float danoMeteoro = 45f;
    public float tempoAvisoMeteoro = 1.2f;
    public float alturaQuedaMeteoro = 10f;
    public GameObject prefabMeteoro; // Opcional: se vazio, usa um fallback simples em código
}