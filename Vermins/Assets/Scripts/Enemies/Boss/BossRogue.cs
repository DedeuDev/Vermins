using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BossRogue : MonoBehaviour
{
    [Header("Configurações")]
    [SerializeField] private BossLadinoDataSO dados;
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

    private Vector3 ultimoPontoFuga = Vector3.zero;

    private void ExecutarComportamentoPadrao(float distancia, float cooldownTiro)
    {
        // 1. Lógica de Fuga Prioritária
        if (distancia < dados.distanciaSegura)
        {
            Vector3 melhorDestino = ObterMelhorPontoFuga();

            if (melhorDestino != Vector3.zero)
            {
                // Reativa o agente se estiver parado
                if (agent.isStopped) 
                    agent.isStopped = false;

                // Só recalcula a rota no NavMesh se o novo ponto for significativamente diferente do atual
                // Isso impede que os golpes continuos do Player cancelem a trajetória de fuga do Boss
                if (Vector3.Distance(ultimoPontoFuga, melhorDestino) > 1.0f || !agent.hasPath)
                {
                    ultimoPontoFuga = melhorDestino;
                    agent.SetDestination(melhorDestino);
                }
            }
        }
        else
        {
            // Se o Player se afastou, limpa o destino de fuga e para
            ultimoPontoFuga = Vector3.zero;
            agent.isStopped = true;
        }

        // Mantém o foco no Player
        LookAtPlayer();

        // 2. Ataque da Besta
        if (timerFlecha >= cooldownTiro && distancia <= dados.alcanceBesta)
        {
            AtirarFlecha();
        }

        // 3. Bomba de Veneno
        if (timerVeneno >= dados.cooldownVeneno)
        {
            ArremessarVeneno();
        }
    }

    private Vector3 ObterMelhorPontoFuga()
    {
        Vector3 direcaoOposta = (transform.position - player.position).normalized;
        
        // Testa direções em leque (Trás, Diagonais e Lados)
        float[] angulos = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };
        float distanciaFuga = 3.5f;

        foreach (float angulo in angulos)
        {
            Vector3 direcaoTeste = Quaternion.Euler(0, angulo, 0) * direcaoOposta;
            Vector3 pontoAlvo = transform.position + direcaoTeste * distanciaFuga;

            // Procura um ponto navegável na malha do NavMesh
            if (NavMesh.SamplePosition(pontoAlvo, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                // Checa se o ponto encontrado realmente afasta o Boss do Player
                float distanciaNovoPontoAoPlayer = Vector3.Distance(hit.position, player.position);
                float distanciaAtualAoPlayer = Vector3.Distance(transform.position, player.position);

                if (distanciaNovoPontoAoPlayer > distanciaAtualAoPlayer)
                {
                    return hit.position;
                }
            }
        }

        return Vector3.zero;
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

        if (dados != null && dados.prefabFlecha != null && pontoDisparoBesta != null && player != null)
        {
            // 1. Força o Boss a virar para o jogador PRIMEIRO
            LookAtPlayer();

            // 2. Calcula a direção exata até o peito/centro do Player
            Vector3 direcao = (player.position + Vector3.up * 1.0f) - pontoDisparoBesta.position;

            if (direcao != Vector3.zero)
            {
                // 3. Trava a rotação final no instante do disparo
                Quaternion rotacaoDisparo = Quaternion.LookRotation(direcao);

                // 4. Instancia a flecha sem vínculo de hierarquia
                Instantiate(dados.prefabFlecha, pontoDisparoBesta.position, rotacaoDisparo);
            }
        }
    }

    private void ArremessarVeneno()
    {
        timerVeneno = 0f;

        if (dados != null && dados.prefabPocaVeneno != null && player != null)
        {
            LookAtPlayer();

            // Origem do arremesso (mão/besta do Boss)
            Vector3 pontoOrigem = pontoDisparoBesta != null ? pontoDisparoBesta.position : transform.position + Vector3.up * 1.5f;

            // Cria a esfera visual do frasco
            GameObject frascoObjeto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            frascoObjeto.transform.position = pontoOrigem;
            frascoObjeto.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            // Remove o collider para não colidir com a cabeça/corpo do Player no ar
            Destroy(frascoObjeto.GetComponent<SphereCollider>());

            // Aplica a cor verde ao frasco
            Renderer rend = frascoObjeto.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = Color.green;
            }

            // Adiciona e inicializa o script de trajetória em arco
            FrascoVenenoProjetil scriptFrasco = frascoObjeto.AddComponent<FrascoVenenoProjetil>();
            scriptFrasco.Inicializar(pontoOrigem, player.position, dados.prefabPocaVeneno);
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