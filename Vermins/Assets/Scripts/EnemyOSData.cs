using UnityEngine;

[CreateAssetMenu(fileName = "NovoTipoInimigo", menuName = "IA/Dados de Inimigo")]
public class DadosInimigoSO : ScriptableObject
{
    [Header("Identificação")]
    public string nomeTipo = "Inimigo Padrão";

    [Header("Movimentação")]
    public float velocidade = 3.5f;
    public float velocidadePerseguicao = 5.0f;

    [Header("Visão e Combate")]
    public float raioDetecao = 8.0f;
    [Range(0f, 360f)]
    public float anguloVisao = 90.0f; // Cone de visão em graus (ex: 90° à frente)
    public float raioAtaque = 1.5f;
    public float tempoEntreAtaques = 1.5f;

    [Header("Comportamento e Timers")]
    public float tempoEsperaPonto = 2.0f;
    public float tempoInvestigacao = 3.0f;
}