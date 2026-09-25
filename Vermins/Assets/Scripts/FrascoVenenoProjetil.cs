using UnityEngine;

public class FrascoVenenoProjetil : MonoBehaviour
{
    [SerializeField] private float velocidade = 8f;
    [SerializeField] private float alturaArco = 3f;
    [SerializeField] private GameObject prefabPocaVeneno;

    private Vector3 pontoInicial;
    private Vector3 pontoDestinoChao;
    private float progresso = 0f;
    private float distanciaTotal;

    public void Inicializar(Vector3 origem, Vector3 alvoPlayer, GameObject pocaPrefab)
    {
        pontoInicial = origem;
        prefabPocaVeneno = pocaPrefab;

        // Calcula a posição do chão ignorando a colisão com o Player
        pontoDestinoChao = EncontrarPontoNoChao(alvoPlayer);
        distanciaTotal = Vector3.Distance(pontoInicial, pontoDestinoChao);
    }

    void Update()
    {
        if (distanciaTotal <= 0) return;

        progresso += (velocidade / distanciaTotal) * Time.deltaTime;

        if (progresso < 1.0f)
        {
            Vector3 posicaoAtual = Vector3.Lerp(pontoInicial, pontoDestinoChao, progresso);
            posicaoAtual.y += Mathf.Sin(progresso * Mathf.PI) * alturaArco;
            transform.position = posicaoAtual;
        }
        else
        {
            CriarPocaNoChao();
        }
    }

    private Vector3 EncontrarPontoNoChao(Vector3 posicaoAlvo)
    {
        // Lança múltiplos raios de cima para baixo na posição do alvo
        RaycastHit[] hits = Physics.RaycastAll(posicaoAlvo + Vector3.up * 2f, Vector3.down, 10f);

        foreach (RaycastHit hit in hits)
        {
            // Ignora o Player e o próprio Boss/Inimigo se o raio acertar algum deles
            if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Enemy") && !hit.collider.CompareTag("Boss"))
            {
                return hit.point; // Retorna o primeiro ponto que for CHÃO / Cenário
            }
        }

        // Caso de segurança se não encontrar nada (mantém na altura dos pés)
        return new Vector3(posicaoAlvo.x, 0.02f, posicaoAlvo.z);
    }

    private void CriarPocaNoChao()
    {
        if (prefabPocaVeneno != null)
        {
            // Instancia a poça ligeiramente acima da superfície do chão (0.02f)
            Vector3 posicaoFinal = pontoDestinoChao + new Vector3(0, 0.02f, 0);
            Instantiate(prefabPocaVeneno, posicaoFinal, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}