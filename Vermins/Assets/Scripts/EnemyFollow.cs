using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class InimigoSeguir : MonoBehaviour
{
    public enum EstadoInimigo { Patrulhando, Perseguindo, Investigando, Atacando }
    
    [Header("Configuração de Tipo (ScriptableObject)")]
    public DadosInimigoSO dados;

    [Header("Estado Atual")]
    public EstadoInimigo estadoAtual = EstadoInimigo.Patrulhando;

    [Header("Referências da Cena")]
    public Transform player;
    public LayerMask camadaObstaculos;
    public Transform[] pontosPatrulha; 
    
    private Vector3 ultimaPosicaoVistaPlayer;
    private bool estaInvestigando = false;
    private int indicePontoAtual = 0;
    private bool estaEsperando = false;

    private NavMeshAgent agent;
    private float tempoUltimoAtaque;

    // Ian: opcional. Se o inimigo nao tiver um MeleeAttack, tudo
    // aqui funciona exatamente como antes.
    private MeleeAttack ataque;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        ataque = GetComponent<MeleeAttack>();

        if (dados != null)
        {
            agent.speed = dados.velocidade;
        }
        else
        {
            Debug.LogWarning($"Dados de Inimigo não atribuídos em {gameObject.name}!");
        }
    }

    void Update()
    {
        if (player == null || dados == null) return;

        float distanciaPlayer = Vector3.Distance(transform.position, player.position);
        bool temVisaoDireta = ChecarLinhaDeVisao(distanciaPlayer);

        // Transição de Estados
        if (distanciaPlayer <= dados.raioAtaque && temVisaoDireta)
        {
            CancelarEspera();
            CancelarInvestigacao();
            estadoAtual = EstadoInimigo.Atacando;
        }
        else if (distanciaPlayer <= dados.raioDetecao && temVisaoDireta)
        {
            CancelarEspera();
            CancelarInvestigacao();
            ultimaPosicaoVistaPlayer = player.position; 
            estadoAtual = EstadoInimigo.Perseguindo;
        }
        else
        {
            if (estadoAtual == EstadoInimigo.Perseguindo)
            {
                estadoAtual = EstadoInimigo.Investigando;
            }
            else if (estadoAtual != EstadoInimigo.Investigando)
            {
                estadoAtual = EstadoInimigo.Patrulhando;
            }
        }

        ExecutingState();
    }

    /// <summary>
    /// Checa se o jogador está dentro do raio, do cone de visão de ângulo (FOV) e sem obstáculos cortando a linha de visão.
    /// </summary>
    bool ChecarLinhaDeVisao(float distanciaPlayer)
    {
        // 1. Checagem de Raio Básico
        if (distanciaPlayer > dados.raioDetecao) return false;

        // 2. Checagem do Ângulo do Cone de Visão (FOV)
        Vector3 direcaoParaPlayer = (player.position - transform.position).normalized;
        float angulo = Vector3.Angle(transform.forward, direcaoParaPlayer);

        // Se o jogador estiver fora da metade do ângulo limite (ex: fora de 45° para cada lado), ignora
        if (angulo > dados.anguloVisao / 2f)
        {
            return false;
        }

        // 3. Checagem de Obstáculos (Physics.Linecast)
        Vector3 olhosInimigo = transform.position + Vector3.up * 0.5f;
        Vector3 centroPlayer = player.position + Vector3.up * 0.5f;

        return !Physics.Linecast(olhosInimigo, centroPlayer, camadaObstaculos);
    }

    void ExecutingState()
    {
        switch (estadoAtual)
        {
            case EstadoInimigo.Patrulhando:
                agent.speed = dados.velocidade;
                ExecutarPatrulha();
                break;

            case EstadoInimigo.Perseguindo:
                agent.isStopped = false;
                agent.speed = dados.velocidadePerseguicao;
                agent.SetDestination(player.position);
                break;

            case EstadoInimigo.Investigando:
                agent.speed = dados.velocidadePerseguicao;
                ExecutarInvestigacao();
                break;

            case EstadoInimigo.Atacando:
                agent.isStopped = true;
                transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));
                
                if (Time.time >= tempoUltimoAtaque + dados.tempoEntreAtaques)
                {
                    Debug.Log($"{dados.nomeTipo} Atacou!");

                    // Ian: aqui o ataque sai do console e vira dano
                    // de verdade. O quanto tira fica no componente
                    // MeleeAttack do proprio inimigo.
                    if (ataque != null)
                        ataque.TryHit(player);
                    tempoUltimoAtaque = Time.time;
                }
                break;
        }
    }

    void ExecutarPatrulha()
    {
        if (pontosPatrulha.Length == 0 || estaEsperando) return;

        agent.isStopped = false;
        agent.SetDestination(pontosPatrulha[indicePontoAtual].position);

        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            StartCoroutine(AguardarNoPonto());
        }
    }

    void ExecutarInvestigacao()
    {
        if (estaInvestigando) return;

        agent.isStopped = false;
        agent.SetDestination(ultimaPosicaoVistaPlayer);

        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            StartCoroutine(AguardarInvestigacao());
        }
    }

    IEnumerator AguardarNoPonto()
    {
        estaEsperando = true;
        agent.isStopped = true;

        yield return new WaitForSeconds(dados.tempoEsperaPonto);

        indicePontoAtual = (indicePontoAtual + 1) % pontosPatrulha.Length;
        estaEsperando = false;
    }

    IEnumerator AguardarInvestigacao()
    {
        estaInvestigando = true;
        agent.isStopped = true;

        yield return new WaitForSeconds(dados.tempoInvestigacao);

        estaInvestigando = false;
        estadoAtual = EstadoInimigo.Patrulhando;
    }

    void CancelarEspera()
    {
        if (estaEsperando)
        {
            StopAllCoroutines();
            estaEsperando = false;
        }
    }

    void CancelarInvestigacao()
    {
        if (estaInvestigando)
        {
            StopAllCoroutines();
            estaInvestigando = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (dados == null) return;

        // Desenha a esfera do Raio de Detecção
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dados.raioDetecao);

        // Desenha a esfera de Ataque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, dados.raioAtaque);

        // Desenha as bordas do Cone de Visão (FOV)
        Vector3 visaoEsquerda = Quaternion.Euler(0, -dados.anguloVisao / 2f, 0) * transform.forward;
        Vector3 visaoDireita = Quaternion.Euler(0, dados.anguloVisao / 2f, 0) * transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, visaoEsquerda * dados.raioDetecao);
        Gizmos.DrawRay(transform.position, visaoDireita * dados.raioDetecao);

        if (player != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, player.position + Vector3.up * 0.5f);
        }

        if (estadoAtual == EstadoInimigo.Investigando)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(ultimaPosicaoVistaPlayer, 0.4f);
        }
    }
}