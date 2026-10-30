using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BossSacerdote : MonoBehaviour
{
    [Header("Configurações")]
    [SerializeField] private BossSacerdoteDataSO dados;
    [SerializeField] private Transform player;

    [Header("Pontos de Efeito")]
    [SerializeField] private Transform pontoArmadilha;

    private NavMeshAgent agent;
    private BossStats stats;

    private float timerArmadilha;
    private bool estaExecutandoAcao = false;

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
    }

    void Update()
    {
        if (player == null || dados == null || estaExecutandoAcao) return;

        timerArmadilha += Time.deltaTime;

        float distanciaPlayer = Vector3.Distance(transform.position, player.position);

        LookAtPlayer();

        // Comportamento do Sacerdote: Tenta manter distância segura e conjurar armadilhas
        if (timerArmadilha >= dados.cooldownArmadilha && distanciaPlayer <= 12f)
        {
            StartCoroutine(RotinaArmadilhaFogo());
            return;
        }

        GerenciarMovimento(distanciaPlayer);
    }

    private void GerenciarMovimento(float distancia)
    {
        agent.isStopped = false;

        // Se o player chegar muito perto, recua; se estiver longe, aproxima-se até a distância segura
        if (distancia < dados.distanciaSegura - 1f)
        {
            Vector3 direcaoFuga = (transform.position - player.position).normalized;
            Vector3 posicaoRecuo = transform.position + direcaoFuga * 3f;
            agent.SetDestination(posicaoRecuo);
        }
        else if (distancia > dados.distanciaSegura + 2f)
        {
            agent.speed = dados.velocidadeCaminhada;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true; // Fica estático na distância ideal conjurando
        }
    }

    private IEnumerator RotinaArmadilhaFogo()
    {
        estaExecutandoAcao = true;
        timerArmadilha = 0f;
        agent.isStopped = true;

        Debug.Log("<color=magenta>[SACERDOTE] Preparando Armadilha Mágica de Fogo!</color>");

        Vector3 posicaoAlvo = player.position;
        posicaoAlvo.y = 0.05f; // Rente ao chão

        // 1. Cria o Círculo Mágico de Aviso no Chão
        GameObject circuloAviso = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(circuloAviso.GetComponent<Collider>());
        circuloAviso.transform.position = posicaoAlvo;
        
        float diametro = dados.raioArmadilha * 2f;
        circuloAviso.transform.localScale = new Vector3(diametro, 0.02f, diametro);

        Renderer renAviso = circuloAviso.GetComponent<Renderer>();
        if (renAviso != null)
        {
            renAviso.material.color = new Color(1f, 0.1f, 0.5f, 0.6f);
        }

        float tempoDecorrido = 0f;
        while (tempoDecorrido < dados.tempoAvisoArmadilha)
        {
            tempoDecorrido += Time.deltaTime;
            float pulso = 1f + Mathf.Sin(Time.time * 15f) * 0.1f;
            circuloAviso.transform.localScale = new Vector3(diametro * pulso, 0.02f, diametro * pulso);
            yield return null;
        }

        Destroy(circuloAviso);

        // 2. DISPARO DO FOGO DO CHÃO PARA CIMA
        Debug.Log("<color=red>[SACERDOTE] FOGO DO CHÃO ATIVADO!</color>");

        if (dados.prefabPilarFogo != null)
        {
            Instantiate(dados.prefabPilarFogo, posicaoAlvo, Quaternion.identity);
        }
        else
        {
            GameObject pilarFallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pilarFallback.GetComponent<Collider>());
            pilarFallback.transform.position = posicaoAlvo + Vector3.up * 2f;
            pilarFallback.transform.localScale = new Vector3(dados.raioArmadilha * 1.5f, 4f, dados.raioArmadilha * 1.5f);
            
            Renderer renPilar = pilarFallback.GetComponent<Renderer>();
            if (renPilar != null) renPilar.material.color = Color.red;
            
            Destroy(pilarFallback, 0.6f);
        }

        // 3. Checa dano no player
        Vector3 posicaoNoChaoPlayer = player.position;
        posicaoNoChaoPlayer.y = posicaoAlvo.y;
        
        if (Vector3.Distance(posicaoAlvo, posicaoNoChaoPlayer) <= dados.raioArmadilha)
        {
            Health vidaPlayer = player.GetComponentInParent<Health>();
            if (vidaPlayer != null)
            {
                vidaPlayer.TakeDamage(dados.danoArmadilha);
                Debug.Log("<color=red>[SACERDOTE] Player incinerado pela Armadilha de Fogo!</color>");
            }
        }

        yield return new WaitForSeconds(0.5f);
        estaExecutandoAcao = false;
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
}