using System;
using UnityEngine;
using UnityEngine.InputSystem; // Adicionado para suporte ao Input System

public class BossStats : MonoBehaviour
{
    public BossDataSO dadosBoss;
    
    private float vidaAtual;
    public event Action<int> OnMudancaFase;
    public event Action OnBossMorto;

    public int FaseAtual { get; private set; } = 1;

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

    void Update()
    {
        // Usa o novo Input System para checar a tecla sem estourar exceção
        if (Keyboard.current != null)
        {
            // Pressione Y para simular dano da Fase 2
            if (Keyboard.current.yKey.wasPressedThisFrame && FaseAtual < 2)
            {
                Debug.Log("<color=yellow>[DEBUG] Pressionou Y: Ativando Fase 2!</color>");
                TomarDano(dadosBoss.vidaMaxima * 0.45f);
            }

            // Pressione U para simular dano da Fase 3
            if (Keyboard.current.uKey.wasPressedThisFrame && FaseAtual < 3)
            {
                Debug.Log("<color=red>[DEBUG] Pressionou U: Ativando Fase 3!</color>");
                TomarDano(dadosBoss.vidaMaxima * 0.75f);
            }
        }
    }

    public void TomarDano(float quantidade)
    {
        vidaAtual -= quantidade;
        vidaAtual = Mathf.Max(vidaAtual, 0f);

        Debug.Log($"[BossStats] Vida Atual: {vidaAtual}/{dadosBoss.vidaMaxima} ({(vidaAtual / dadosBoss.vidaMaxima) * 100f}%)");

        VerificarMudancaFase();

        if (vidaAtual <= 0)
        {
            OnBossMorto?.Invoke();
            Destroy(gameObject);
        }
    }

    private void VerificarMudancaFase()
    {
        if (dadosBoss == null || dadosBoss.vidaMaxima <= 0) return;

        float porcentagemVida = (vidaAtual / dadosBoss.vidaMaxima) * 100f;

        if (porcentagemVida <= 30f && FaseAtual != 3)
        {
            FaseAtual = 3;
            Debug.Log("<color=orange>[BOSS] MUDANÇA PARA A FASE 3!</color>");
            OnMudancaFase?.Invoke(3);
        }
        else if (porcentagemVida <= 60f && FaseAtual < 2)
        {
            FaseAtual = 2;
            Debug.Log("<color=yellow>[BOSS] MUDANÇA PARA A FASE 2!</color>");
            OnMudancaFase?.Invoke(2);
        }
    }

    public float GetPorcentagemVida() => (vidaAtual / (dadosBoss != null ? dadosBoss.vidaMaxima : 1f));
}