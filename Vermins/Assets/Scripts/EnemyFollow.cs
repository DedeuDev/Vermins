using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class InimigoSeguir : MonoBehaviour
{
    public enum EstadoInimigo { Patrulhando, Perseguindo, Atacando }
    
    [Header("Estado Atual")]
    public EstadoInimigo estadoAtual = EstadoInimigo.Patrulhando;

    [Header("Configurações de Distância e Visão")]
    public Transform player;
    public float raioDetecao = 8f;   
    public float raioAtaque = 1.5f;   
    public float tempoEntreAtaques = 1.5f;
    public LayerMask camadaObstaculos;

    [Header("Patrulha por Waypoints")]
    public Transform[] pontosPatrulha; 
    public float distanciaPonto = 0.5f; 
    public float tempoEsperaPonto = 2.0f; // Tempo em segundos que o inimigo espera em cada ponto
    
    private int indicePontoAtual = 0;
    private bool estaEsperando = false; // Controle de pausa na patrulha

    private NavMeshAgent agent;
    private float tempoUltimoAtaque;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (player == null) return;

        float distanciaPlayer = Vector3.Distance(transform.position, player.position);
        bool temVisaoDireta = ChecarLinhaDeVisao();

        // Transição de Estados
        if (distanciaPlayer <= raioAtaque && temVisaoDireta)
        {
            CancelarEspera();
            estadoAtual = EstadoInimigo.Atacando;
        }
        else if (distanciaPlayer <= raioDetecao && temVisaoDireta)
        {
            CancelarEspera();
            estadoAtual = EstadoInimigo.Perseguindo;
        }
        else
        {
            estadoAtual = EstadoInimigo.Patrulhando;
        }

        ExecutingState();
    }

    bool ChecarLinhaDeVisao()
    {
        Vector3 olhosInimigo = transform.position + Vector3.up * 0.5f;
        Vector3 centroPlayer = player.position + Vector3.up * 0.5f;

        if (Physics.Linecast(olhosInimigo, centroPlayer, camadaObstaculos))
        {
            return false; 
        }

        return true; 
    }

    void ExecutingState()
    {
        switch (estadoAtual)
        {
            case EstadoInimigo.Patrulhando:
                ExecutarPatrulha();
                break;

            case EstadoInimigo.Perseguindo:
                agent.isStopped = false;
                agent.SetDestination(player.position);
                break;

            case EstadoInimigo.Atacando:
                agent.isStopped = true;
                transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));
                
                if (Time.time >= tempoUltimoAtaque + tempoEntreAtaques)
                {
                    Debug.Log("Inimigo Atacou!");
                    tempoUltimoAtaque = Time.time;
                }
                break;
        }
    }

    void ExecutarPatrulha()
    {
        if (pontosPatrulha.Length == 0) return;

        // Se estiver no tempo de pausa do ponto, não calcula novas rotas
        if (estaEsperando) return;

        agent.isStopped = false;
        agent.SetDestination(pontosPatrulha[indicePontoAtual].position);

        // Verifica se chegou ao ponto
        if (!agent.pathPending && agent.remainingDistance <= distanciaPonto)
        {
            StartCoroutine(AguardarNoPonto());
        }
    }

    // Corrotina que gerencia o tempo de espera no waypoint
    IEnumerator AguardarNoPonto()
    {
        estaEsperando = true;
        agent.isStopped = true; // Para de andar durante a espera

        yield return new WaitForSeconds(tempoEsperaPonto);

        // Escolhe o próximo ponto após o tempo de espera
        indicePontoAtual = (indicePontoAtual + 1) % pontosPatrulha.Length;
        estaEsperando = false;
    }

    // Garante que a espera seja cancelada se o inimigo avistar o player
    void CancelarEspera()
    {
        if (estaEsperando)
        {
            StopAllCoroutines();
            estaEsperando = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, raioDetecao);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, raioAtaque);

        if (player != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, player.position + Vector3.up * 0.5f);
        }
    }
}