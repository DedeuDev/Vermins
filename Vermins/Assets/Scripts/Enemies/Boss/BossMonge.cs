using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BossMonge : MonoBehaviour
{
    [Header("Configurações")]
    [SerializeField] private BossMongeDataSO dados;
    [SerializeField] private Transform player;

    [Header("Referências Visuais e Pontos de Efeito")]
    [SerializeField] private Transform pontoImpactoTerremoto;
    [SerializeField] private Transform pontoEscudoKi;

    private NavMeshAgent agent;
    private BossStats stats;

    private float timerAtaqueMelee;
    private float timerTerremoto;
    private float timerEscudoKi;

    private bool estaExecutandoAcao = false;
    private bool escudoAtivo = false;
    private GameObject instanciaEscudo;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<BossStats>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (dados != null && agent != null)
        {
            agent.speed = dados.velocidadeCaminhada;
        }

        if (stats != null)
        {
            stats.OnMudancaFase += TratarMudancaFase;
        }
    }

    void Update()
    {
        if (player == null || dados == null || estaExecutandoAcao) return;

        AtualizarTimers();

        float distanciaPlayer = Vector3.Distance(transform.position, player.position);

        LookAtPlayer();

        if (stats != null && stats.FaseAtual == 1)
        {
            ExecutarComportamentoFase1(distanciaPlayer);
        }
        else
        {
            ExecutarComportamentoFase2(distanciaPlayer);
        }
    }

    private void AtualizarTimers()
    {
        timerAtaqueMelee += Time.deltaTime;
        timerTerremoto += Time.deltaTime;
        timerEscudoKi += Time.deltaTime;
    }

    private void ExecutarComportamentoFase1(float distancia)
    {
        // 1. Habilidade: Terremoto (se o jogador estiver no alcance e cooldown pronto)
        if (timerTerremoto >= dados.cooldownTerremoto && distancia <= dados.alcanceDecisaoTerremoto)
        {
            StartCoroutine(RotinaTerremoto());
            return;
        }

        // 2. Ataque Melee
        if (distancia <= dados.alcanceAtaqueMelee)
        {
            agent.isStopped = true;

            if (timerAtaqueMelee >= dados.cooldownAtaqueMelee)
            {
                AtacarMelee();
            }
        }
        else
        {
            // Persegue o jogador
            agent.isStopped = false;
            agent.speed = dados.velocidadeCaminhada;
            agent.SetDestination(player.position);
        }
    }

    private void ExecutarComportamentoFase2(float distancia)
    {
        // Na Fase 2 ele fica mais rápido ao se aproximar
        agent.speed = dados.velocidadeAproximacao;

        // Ativa o Escudo de Ki periodicamente
        if (timerEscudoKi >= dados.cooldownEscudoKi && !escudoAtivo)
        {
            StartCoroutine(RotinaEscudoKi());
        }

        // Executa a lógica de perseguição/ataques da fase 1
        ExecutarComportamentoFase1(distancia);
    }

    #region Habilidades

    private void AtacarMelee()
    {
        timerAtaqueMelee = 0f;
        Debug.Log("<color=cyan>[MONGE] Executou Ataque Melee!</color>");

        // Aplica dano direto se o Player estiver no alcance
        if (Vector3.Distance(transform.position, player.position) <= dados.alcanceAtaqueMelee)
        {
            Health vidaPlayer = player.GetComponentInParent<Health>();
            if (vidaPlayer != null)
            {
                vidaPlayer.TakeDamage(dados.danoMelee);
            }
        }
    }

    private IEnumerator RotinaTerremoto()
    {
        estaExecutandoAcao = true;
        timerTerremoto = 0f;
        agent.isStopped = true;

        Debug.Log("<color=orange>[MONGE] Canalizando TERREMOTO!</color>");

        yield return new WaitForSeconds(0.6f);

        Vector3 posicaoImpacto = pontoImpactoTerremoto != null ? pontoImpactoTerremoto.position : transform.position;

        if (dados.prefabEfeitoTerremoto != null)
        {
            Instantiate(dados.prefabEfeitoTerremoto, posicaoImpacto, Quaternion.identity);
        }

        if (Vector3.Distance(posicaoImpacto, player.position) <= dados.raioImpactoTerremoto)
        {
            Health vidaPlayer = player.GetComponentInParent<Health>();
            if (vidaPlayer != null)
            {
                vidaPlayer.TakeDamage(dados.danoTerremoto);
                Debug.Log("<color=red>[MONGE] Player atingido pelo Terremoto!</color>");
            }
        }

        yield return new WaitForSeconds(0.8f);
        estaExecutandoAcao = false;
    }

    private IEnumerator RotinaEscudoKi()
    {
        escudoAtivo = true;
        timerEscudoKi = 0f;

        Debug.Log("<color=green>[MONGE] Ativou Escudo de Ki!</color>");

        if (dados.prefabEscudoKi != null)
        {
            Transform pontoPai = pontoEscudoKi != null ? pontoEscudoKi : transform;
            instanciaEscudo = Instantiate(dados.prefabEscudoKi, pontoPai.position, Quaternion.identity, pontoPai);
        }

        yield return new WaitForSeconds(dados.duracaoEscudoKi);

        if (instanciaEscudo != null)
        {
            Destroy(instanciaEscudo);
        }

        escudoAtivo = false;
        Debug.Log("<color=gray>[MONGE] Escudo de Ki expirou.</color>");
    }

    private void TratarMudancaFase(int novaFase)
    {
        if (novaFase == 2)
        {
            Debug.Log("<color=yellow>[MONGE] ENTROU NA FASE 2: FÚRIA DO KI!</color>");
            StartCoroutine(RotinaEscudoKi());
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