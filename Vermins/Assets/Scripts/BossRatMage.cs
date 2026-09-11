using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BossRatMage : MonoBehaviour
{
    public enum FormaBoss { Mago, Besta }

    [Header("Configurações")]
    [SerializeField] private BossDataSO dados;
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask camadaObstaculos;

    [Header("Componentes de Apresentação/Modelos")]
    [SerializeField] private GameObject modeloMago;
    [SerializeField] private GameObject modeloBesta;
    [SerializeField] private Transform pontoDisparoProjetil;

    private NavMeshAgent agent;
    private BossStats stats;
    private FormaBoss formaAtual = FormaBoss.Mago;

    // Timers de Cooldowns
    private float timerProjetil;
    private float timerTeleporte;
    private float timerInvocacao;
    private float timerAtaqueBesta;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<BossStats>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (stats != null)
        {
            stats.OnMudancaFase += TratarMudancaFase;
        }

        AlternarForma(FormaBoss.Mago);
    }

    void Update()
    {
        if (player == null || dados == null) return;

        AtualizarTimers();

        float distanciaPlayer = Vector3.Distance(transform.position, player.position);

        switch (stats.FaseAtual)
        {
            case 1:
                ExecutarFase1_Mago(distanciaPlayer);
                break;
            case 2:
                ExecutarFase2_Transmorfo(distanciaPlayer);
                break;
            case 3:
                ExecutarFase3_BestaBerseker(distanciaPlayer);
                break;
        }
    }

    private void AtualizarTimers()
    {
        timerProjetil += Time.deltaTime;
        timerTeleporte += Time.deltaTime;
        timerInvocacao += Time.deltaTime;
        timerAtaqueBesta += Time.deltaTime;
    }

    #region Lógica de Fases

    // FASE 1: Mantém distância e ataca de longe
    private void ExecutarFase1_Mago(float distancia)
    {
        if (formaAtual != FormaBoss.Mago) AlternarForma(FormaBoss.Mago);

        if (distancia < dados.distanciaSeguraMago)
        {
            Vector3 direcaoOposta = (transform.position - player.position).normalized;
            Vector3 destinoRecuo = transform.position + direcaoOposta * 2f;

            // Tenta recuar em linha reta
            if (NavMesh.SamplePosition(destinoRecuo, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
            else
            {
                // Se a retaguarda estiver bloqueada por parede, tenta escapar para a ESQUERDA ou DIREITA
                Vector3 fugaEsquerda = transform.position + Quaternion.Euler(0, 60, 0) * direcaoOposta * 3f;
                Vector3 fugaDireita = transform.position + Quaternion.Euler(0, -60, 0) * direcaoOposta * 3f;

                if (NavMesh.SamplePosition(fugaEsquerda, out NavMeshHit hitEsq, 2f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hitEsq.position);
                }
                else if (NavMesh.SamplePosition(fugaDireita, out NavMeshHit hitDir, 2f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hitDir.position);
                }
            }

            // O teleporte agora exige COOLDOWN e que ele NÃO consiga se mover por um tempo (pregado na parede)
            if (distancia <= 2.0f && timerTeleporte >= dados.cooldownTeleporte)
            {
                ExecutarTeleporte();
            }
        }
        else
        {
            agent.isStopped = true;
            LookAtPlayer();

            if (timerProjetil >= dados.cooldownProjetil && distancia <= dados.alcanceProjetil)
            {
                LancarProjetil();
            }
        }
    }

    // FASE 2: Híbrido, invoca minions e se transforma temporariamente
    private void ExecutarFase2_Transmorfo(float distancia)
    {
        // Invocação de Minions
        if (timerInvocacao >= dados.cooldownInvocacao)
        {
            InvocarMinions();
        }

        // Se o player estiver muito perto, vira Besta para atacar Melee
        if (distancia <= dados.alcanceAtaqueBesta * 2f)
        {
            if (formaAtual != FormaBoss.Besta) AlternarForma(FormaBoss.Besta);

            agent.isStopped = false;
            agent.SetDestination(player.position);

            if (distancia <= dados.alcanceAtaqueBesta && timerAtaqueBesta >= dados.cooldownAtaqueBesta)
            {
                AtaqueMeleeBesta();
            }
        }
        else
        {
            // Volta a ser mago e ataca de longe
            if (formaAtual != FormaBoss.Mago) AlternarForma(FormaBoss.Mago);
            ExecutarFase1_Mago(distancia);
        }
    }

    // FASE 3: Totalmente Besta, ultra agressivo
    private void ExecutarFase3_BestaBerseker(float distancia)
    {
        if (formaAtual != FormaBoss.Besta) AlternarForma(FormaBoss.Besta);

        agent.isStopped = false;
        agent.speed = dados.velocidadeBesta;
        agent.SetDestination(player.position);

        if (distancia <= dados.alcanceAtaqueBesta && timerAtaqueBesta >= dados.cooldownAtaqueBesta)
        {
            AtaqueMeleeBesta();
        }
    }

    #endregion

    #region Habilidades

    private void LancarProjetil()
    {
        timerProjetil = 0f;
        if (dados.prefabProjetil != null && pontoDisparoProjetil != null)
        {
            Instantiate(dados.prefabProjetil, pontoDisparoProjetil.position, transform.rotation);
        }
    }

    private void ExecutarTeleporte()
    {
        timerTeleporte = 0f;
        Vector3 randomDirection = Random.insideUnitSphere * dados.raioTeleporte;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, dados.raioTeleporte, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
    }

    private void InvocarMinions()
    {
        timerInvocacao = 0f;
        if (dados.prefabMinion == null) return;

        for (int i = 0; i < dados.quantidadeMinions; i++)
        {
            Vector3 spawnPos = transform.position + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
            Instantiate(dados.prefabMinion, spawnPos, Quaternion.identity);
        }
    }

    private void AtaqueMeleeBesta()
    {
        timerAtaqueBesta = 0f;
        Debug.Log($"{dados.nomeBoss} atacou com a garra da Besta!");

        // Pode reutilizar seu script MeleeAttack aqui
        MeleeAttack ataque = GetComponent<MeleeAttack>();
        if (ataque != null && player != null)
        {
            ataque.TryHit(player);
        }
    }

    #endregion

    #region Auxiliares e Transmorfismo

    private void AlternarForma(FormaBoss novaForma)
    {
        formaAtual = novaForma;

        if (novaForma == FormaBoss.Mago)
        {
            if (modeloMago != null) modeloMago.SetActive(true);
            if (modeloBesta != null) modeloBesta.SetActive(false);
            agent.speed = dados.velocidadeMago;
        }
        else
        {
            if (modeloMago != null) modeloMago.SetActive(false);
            if (modeloBesta != null) modeloBesta.SetActive(true);
            agent.speed = dados.velocidadeBesta;
        }
    }

    private void TratarMudancaFase(int novaFase)
    {
        Debug.Log($"O Boss entrou na FASE {novaFase}!");
        ExecutarTeleporte(); // Teleporta ao mudar de fase para dar feedback
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