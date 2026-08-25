using UnityEngine;
using UnityEngine.AI; // Necessário para acessar o NavMeshAgent

public class InimigoSeguir : MonoBehaviour
{
    public Transform player; // Referência para a posição do jogador
    private NavMeshAgent agent;

    void Start()
    {
        // Pega o componente NavMeshAgent do próprio Inimigo
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // Se a referência do Player estiver configurada, atualiza o destino da navegação
        if (player != null)
        {
            agent.SetDestination(player.position);
        }
    }
}