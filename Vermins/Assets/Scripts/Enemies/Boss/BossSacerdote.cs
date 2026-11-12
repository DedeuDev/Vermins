using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BossSacerdote : MonoBehaviour
{
    [SerializeField] private BossSacerdoteDataSO dados;
    
    private NavMeshAgent agent;
    private Transform player;
    
    private float timerArmadilha = 0f;
    private float timerZoltraak = 0f;
    private bool estaExecutandoAcao = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        GameObject jogadorObj = GameObject.FindGameObjectWithTag("Player");
        if (jogadorObj != null)
        {
            player = jogadorObj.transform;
        }

        if (dados != null)
        {
            agent.speed = dados.velocidadeCaminhada;
        }
    }

    private void Update()
    {
        if (player == null || dados == null || estaExecutandoAcao)
            return;

        float distancia = Vector3.Distance(transform.position, player.position);

        // Atualiza os timers das habilidades
        timerArmadilha += Time.deltaTime;
        timerZoltraak += Time.deltaTime;

        // Prioridade 1: Zoltraak (Agora pode ser usado a qualquer distância quando o cooldown zerar)
        if (timerZoltraak >= dados.cooldownZoltraak)
        {
            StartCoroutine(RotinaZoltraak());
            timerZoltraak = 0f;
            return;
        }

        // Prioridade 2: Chuva de Fogo (Controle de área)
        if (timerArmadilha >= dados.cooldownArmadilha)
        {
            StartCoroutine(RotinaArmadilhaFogo());
            timerArmadilha = 0f;
            return;
        }

        // Movimentação tática: Mantém distância segura do player se ele chegar muito perto
        if (distancia < dados.distanciaSegura)
        {
            Vector3 direcaoFuga = (transform.position - player.position).normalized;
            Vector3 posicaoRecuo = transform.position + direcaoFuga * 4f;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(posicaoRecuo, out hit, 4f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
        else
        {
            agent.SetDestination(player.position);
        }
    }

    // --- CHUVA DE FOGO (Área) ---
    private IEnumerator RotinaArmadilhaFogo()
    {
        estaExecutandoAcao = true;
        agent.isStopped = true;

        int quantidadeArmadilhas = 10;
        float raioEspalhamento = 8f;
        Vector3 centroArena = player.position;
        List<Vector3> posicoesAlvo = new List<Vector3>();
        List<GameObject> circulosAviso = new List<GameObject>();
        float raioIndividual = dados.raioArmadilha * 0.6f;

        for (int i = 0; i < quantidadeArmadilhas; i++)
        {
            Vector2 offsetAleatorio = Random.insideUnitCircle * raioEspalhamento;
            Vector3 posicaoAlvo = centroArena + new Vector3(offsetAleatorio.x, 0.05f, offsetAleatorio.y);
            posicoesAlvo.Add(posicaoAlvo);

            GameObject circuloAviso = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(circuloAviso.GetComponent<Collider>());
            circuloAviso.transform.position = posicaoAlvo;
            
            float diametro = raioIndividual * 2f;
            circuloAviso.transform.localScale = new Vector3(diametro, 0.02f, diametro);

            Renderer renAviso = circuloAviso.GetComponent<Renderer>();
            if (renAviso != null) renAviso.material.color = new Color(1f, 0.1f, 0.5f, 0.6f);

            circulosAviso.Add(circuloAviso);
        }

        float tempoDecorrido = 0f;
        while (tempoDecorrido < dados.tempoAvisoArmadilha)
        {
            tempoDecorrido += Time.deltaTime;
            float pulso = 1f + Mathf.Sin(Time.time * 18f) * 0.1f;
            
            foreach (var circulo in circulosAviso)
            {
                if (circulo != null)
                {
                    float diametroOriginal = raioIndividual * 2f;
                    circulo.transform.localScale = new Vector3(diametroOriginal * pulso, 0.02f, diametroOriginal * pulso);
                }
            }
            yield return null;
        }

        foreach (var circulo in circulosAviso)
        {
            if (circulo != null) Destroy(circulo);
        }

        foreach (var posicaoAlvo in posicoesAlvo)
        {
            if (dados.prefabPilarFogo != null)
            {
                GameObject pilar = Instantiate(dados.prefabPilarFogo, posicaoAlvo, Quaternion.identity);
                pilar.transform.localScale = Vector3.one * 0.6f;
            }
            else
            {
                GameObject pilarFallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(pilarFallback.GetComponent<Collider>());
                pilarFallback.transform.position = posicaoAlvo + Vector3.up * 2f;
                pilarFallback.transform.localScale = new Vector3(raioIndividual * 1.5f, 4f, raioIndividual * 1.5f);
                
                Renderer renPilar = pilarFallback.GetComponent<Renderer>();
                if (renPilar != null) renPilar.material.color = Color.red;
                
                Destroy(pilarFallback, 0.6f);
            }

            Vector3 posicaoNoChaoPlayer = player.position;
            posicaoNoChaoPlayer.y = posicaoAlvo.y;
            
            if (Vector3.Distance(posicaoAlvo, posicaoNoChaoPlayer) <= raioIndividual)
            {
                Health vidaPlayer = player.GetComponentInParent<Health>();
                if (vidaPlayer != null) vidaPlayer.TakeDamage(dados.danoArmadilha);
            }
        }

        yield return new WaitForSeconds(0.4f);
        agent.isStopped = false;
        estaExecutandoAcao = false;
    }

    // --- ZOLTRAAK (Raio Perfurante) ---
    private IEnumerator RotinaZoltraak()
    {
        estaExecutandoAcao = true;
        agent.isStopped = true;

        Vector3 direcaoTiro = (player.position - transform.position).normalized;
        direcaoTiro.y = 0f;
        transform.rotation = Quaternion.LookRotation(direcaoTiro);

        Debug.Log("<color=cyan>[SACERDOTE] Preparando ZOLTRAAK!</color>");

        GameObject linhaAviso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(linhaAviso.GetComponent<Collider>());
        
        float comprimentoRaio = 15f;
        linhaAviso.transform.position = transform.position + direcaoTiro * (comprimentoRaio / 2f) + Vector3.up * 0.05f;
        linhaAviso.transform.rotation = Quaternion.LookRotation(direcaoTiro);
        linhaAviso.transform.localScale = new Vector3(0.4f, 0.02f, comprimentoRaio);

        Renderer renLinha = linhaAviso.GetComponent<Renderer>();
        if (renLinha != null) renLinha.material.color = new Color(0f, 0.8f, 1f, 0.7f);

        yield return new WaitForSeconds(dados.tempoAvisoZoltraak);

        if (linhaAviso != null) Destroy(linhaAviso);

        Debug.Log("<color=blue>[SACERDOTE] ZOLTRAAK DISPARADO!</color>");

        if (dados.prefabRaioZoltraak != null)
        {
            Instantiate(dados.prefabRaioZoltraak, transform.position + Vector3.up * 1f, Quaternion.LookRotation(direcaoTiro));
        }
        else
        {
            GameObject feixe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(feixe.GetComponent<Collider>());
            feixe.transform.position = transform.position + direcaoTiro * (comprimentoRaio / 2f) + Vector3.up * 1f;
            feixe.transform.rotation = Quaternion.LookRotation(direcaoTiro);
            feixe.transform.localScale = new Vector3(0.6f, 0.6f, comprimentoRaio);
            
            Renderer renFeixe = feixe.GetComponent<Renderer>();
            if (renFeixe != null) renFeixe.material.color = Color.cyan;
            
            Destroy(feixe, 0.2f);
        }

        Vector3 posicaoPlayerRelativa = player.position - transform.position;
        float distanciaProjecao = Vector3.Dot(posicaoPlayerRelativa, direcaoTiro);
        
        if (distanciaProjecao >= 0f && distanciaProjecao <= comprimentoRaio)
        {
            Vector3 pontoProjetado = transform.position + direcaoTiro * distanciaProjecao;
            float desvioLateral = Vector3.Distance(player.position, pontoProjetado);

            if (desvioLateral <= 1.0f)
            {
                Health vidaPlayer = player.GetComponentInParent<Health>();
                if (vidaPlayer != null)
                {
                    vidaPlayer.TakeDamage(dados.danoZoltraak);
                    Debug.Log("<color=red>[SACERDOTE] Player atingido em cheio pelo Zoltraak!</color>");
                }
            }
        }

        yield return new WaitForSeconds(0.5f);
        agent.isStopped = false;
        estaExecutandoAcao = false;
    }
}