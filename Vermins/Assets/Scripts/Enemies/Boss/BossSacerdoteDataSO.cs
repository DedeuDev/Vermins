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
}