using System;
using UnityEngine;

public class BossStats : MonoBehaviour
{
    public BossDataSO dadosBoss;
    
    private float vidaAtual;
    public event Action<int> OnMudancaFase; // Evento disparado quando entra na Fase 1, 2 ou 3
    public event Action OnBossMorto;

    public int FaseAtual { get; private set; } = 1;

    // Ian: o PlayerCombat so mira quem tem Health. O VidaDoBoss poe um
    // Health aqui e repassa cada dano pro TomarDano, entao as fases e a
    // morte continuam contigo. Crio ele sozinho pra ninguem precisar
    // lembrar de adicionar no boss.
    void Awake()
    {
        if (GetComponent<VidaDoBoss>() == null)
            gameObject.AddComponent<VidaDoBoss>();
    }

    void Start()
    {
        if (dadosBoss != null)
        {
            vidaAtual = dadosBoss.vidaMaxima;
        }
    }

    public void TomarDano(float quantidade)
    {
        vidaAtual -= quantidade;
        vidaAtual = Mathf.Max(vidaAtual, 0f);

        VerificarMudancaFase();

        if (vidaAtual <= 0)
        {
            OnBossMorto?.Invoke();
            Destroy(gameObject);
        }
    }

    private void VerificarMudancaFase()
    {
        float porcentagemVida = (vidaAtual / dadosBoss.vidaMaxima) * 100f;

        if (porcentagemVida <= 30f && FaseAtual != 3)
        {
            FaseAtual = 3;
            OnMudancaFase?.Invoke(3);
        }
        else if (porcentagemVida <= 60f && FaseAtual < 2)
        {
            FaseAtual = 2;
            OnMudancaFase?.Invoke(2);
        }
    }

    public float GetPorcentagemVida() => (vidaAtual / dadosBoss.vidaMaxima);
}