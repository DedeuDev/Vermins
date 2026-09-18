using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BossRogue : MonoBehaviour
{
    [Header("Configurações")]
    [SerializeField] private BossDataSO dados;
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask camadaObstaculos;

    [Header("Referências Visuais")]
    [SerializeField] private Renderer[] modeloRenderers; // Todos os renderers visuais do Boss
    [SerializeField] private Transform pontoDisparoBesta;

    private NavMeshAgent agent;
    private BossStats stats;

    private float timerFlecha;
    private float timerVeneno;
    private float timerInvisibilidade;

    private bool estaInvisivel = false;
    private bool ativouInvisibilidadeFase2 = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<BossStats>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (dados != null)
        {
            agent.speed = dados.velocidadeNativo;
        }

        if (stats != null)
        {
            stats.OnMudancaFase += TratarMudancaFase;
        }

        // Caso os Renderers não tenham sido associados manualmente, pega nos filhos
        if (modeloRenderers == null || modeloRenderers.Length == 0)
        {
            modeloRenderers = GetComponentsInChildren<Renderer>();
        }
    }

    void Update()
    {
        if (player == null || dados == null || estaInvisivel) return;

        AtualizarTimers();

        float distanciaPlayer = Vector3.Distance(transform.position, player.position);

        if (stats.FaseAtual == 1)
        {
            ExecutarComportamentoPadrao(distanciaPlayer, dados.cooldownFlechaFase1);
        }
        else
        {
            ExecutarFase2_Assassino(distanciaPlayer);
        }
    }

    private void AtualizarTimers()
    {
        timerFlecha += Time.deltaTime;
        timerVeneno += Time.deltaTime;
        timerInvisibilidade += Time.deltaTime;
    }

    private void ExecutarComportamentoPadrao(float distancia, float cooldownTiro)
    {
        // 1. Manter distância (Kiting) com esquiva lateral se encurralado
        if (distancia < dados.distanciaSegura)
        {
            Vector3 direcaoOposta = (transform.position - player.position).normalized;
            Vector3 destinoRecuo = transform.position + direcaoOposta * 2.5f;

            if (NavMesh.SamplePosition(destinoRecuo, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
            else
            {
                // Flanquear para os lados se a retaguarda estiver bloqueada
                Vector3 fugaEsquerda = transform.position + Quaternion.Euler(0, 60, 0) * direcaoOposta * 3f;
                if (NavMesh.SamplePosition(fugaEsquerda, out NavMeshHit hitEsq, 2f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hitEsq.position);
                }
            }
        }
        else
        {
            agent.isStopped = true;
            LookAtPlayer();
        }

        // 2. Disparo da Besta
        if (timerFlecha >= cooldownTiro && distancia <= dados.alcanceBesta)
        {
            AtirarFlecha();
        }

        // 3. Arremesso de Bomba de Veneno
        if (timerVeneno >= dados.cooldownVeneno)
        {
            ArremessarVeneno();
        }
    }

    private void ExecutarFase2_Assassino(float distancia)
    {
        ExecutarComportamentoPadrao(distancia, dados.cooldownFlechaFase2);

        // Habilidade de sumir e reaparecer periodicamente na Fase 2
        if (timerInvisibilidade >= dados.cooldownInvisibilidadeFase2)
        {
            StartCoroutine(RotinaInvisibilidadeEReposicionamento());
        }
    }

    #region Habilidades

    private void AtirarFlecha()
{
    timerFlecha = 0f;

    // 1. Garante que o Boss gire imediatamente para o Player antes de disparar
    LookAtPlayer();

    if (dados != null && dados.prefabFlecha != null && pontoDisparoBesta != null)
    {
        // 2. Calcula a direção exata do ponto de disparo até a posição do Player (na altura do peito/centro)
        Vector3 direcaoParaPlayer = (player.position + Vector3.up * 1.0f) - pontoDisparoBesta.position;
        Quaternion rotacaoTiro = Quaternion.LookRotation(direcaoParaPlayer);

        // 3. Instancia a flecha já virada na direção exata do Player
        Instantiate(dados.prefabFlecha, pontoDisparoBesta.position, rotacaoTiro);

        Debug.Log("Boss mirou e atirou no Player!");
    }
}

    private void ArremessarVeneno()
    {
        timerVeneno = 0f;
        if (dados.prefabPocaVeneno != null && player != null)
        {
            // Cria a poça de veneno diretamente aos pés do jogador
            Instantiate(dados.prefabPocaVeneno, player.position, Quaternion.identity);
        }
    }

    private IEnumerator RotinaInvisibilidadeEReposicionamento()
    {
        estaInvisivel = true;
        timerInvisibilidade = 0f;

        // Efeito visual de bomba de fumaça na posição original
        CriarEfeitoFumaca(transform.position);

        // Oculta os visuais do Boss
        DefinirVisibilidade(false);

        // Aumenta a velocidade enquanto furtivo
        agent.speed = dados.velocidadeFurtivo;

        // Escolhe um ponto de emboscada ao redor do jogador
        Vector3 direcaoAleatoria = Random.insideUnitSphere * 6f;
        direcaoAleatoria.y = 0;
        Vector3 pontoEmboscada = player.position + direcaoAleatoria;

        if (NavMesh.SamplePosition(pontoEmboscada, out NavMeshHit hit, 6f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }

        // Aguarda o tempo de deslocamento invisível
        yield return new WaitForSeconds(dados.tempoInvisivel);

        // Reaparece com fumaça
        CriarEfeitoFumaca(transform.position);
        DefinirVisibilidade(true);

        agent.speed = dados.velocidadeNativo;
        estaInvisivel = false;
        LookAtPlayer();
    }

    private void DefinirVisibilidade(bool visivel)
    {
        foreach (Renderer r in modeloRenderers)
        {
            if (r != null) r.enabled = visivel;
        }
    }

    private void CriarEfeitoFumaca(Vector3 posicao)
    {
        if (dados.prefabEfeitoFumaca != null)
        {
            Instantiate(dados.prefabEfeitoFumaca, posicao, Quaternion.identity);
        }
    }

    private void TratarMudancaFase(int novaFase)
    {
        if (novaFase == 2 && !ativouInvisibilidadeFase2)
        {
            ativouInvisibilidadeFase2 = true;
            StartCoroutine(RotinaInvisibilidadeEReposicionamento());
        }
    }

    private void LookAtPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnMudancaFase -= TratarMudancaFase;
        }
    }

    #endregion
}