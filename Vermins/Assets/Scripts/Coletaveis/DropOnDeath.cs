using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Solta itens no chao quando o dono morre.
///
/// Vai em qualquer coisa que tenha Health - hoje o inimigo, depois
/// barril ou chefe - e nao mexe na IA de ninguem: so escuta o OnDied,
/// do mesmo jeito que o FlashDeDano escuta o OnDamaged. Por isso o
/// EnemyFollow do Leo nao precisou mudar.
///
/// Cada item da lista sorteia sozinho. Quando o ouro existir, ele entra
/// na lista do lado da pocao, e os dois podem cair na mesma morte.
/// </summary>
[RequireComponent(typeof(Health))]
public class DropOnDeath : MonoBehaviour
{
    [System.Serializable]
    public struct Drop
    {
        public GameObject prefab;

        [Tooltip("Chance de cair a cada morte, de 0 a 1. 1 = sempre.")]
        [Range(0f, 1f)] public float chance;
    }

    [SerializeField] private Drop[] drops;

    [Tooltip("Raio em volta do corpo onde os itens caem, pra dois itens " +
             "nao nascerem um dentro do outro.")]
    [SerializeField] private float scatterRadius = 0.5f;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.OnDied += Soltar;
    }

    private void OnDisable()
    {
        health.OnDied -= Soltar;
    }

    private void Soltar(Health _)
    {
        // O Transform do inimigo fica no meio do corpo, 1 m acima do pe
        // (o baseOffset do agent). Procuro o chao a partir do pe: do meio
        // do corpo, a borda de uma plataforma ao lado pode estar mais
        // perto que o chao embaixo, e a pocao cairia la em cima.
        Vector3 pe = transform.position;
        if (TryGetComponent(out NavMeshAgent agent))
            pe -= Vector3.up * agent.baseOffset;

        foreach (Drop drop in drops)
        {
            // O Random.value vai de 0 a 1 com as duas pontas incluidas:
            // chance 1 cai sempre, e o "<= 0" garante que 0 nunca cai.
            if (drop.prefab == null || drop.chance <= 0f || Random.value > drop.chance)
                continue;

            Vector2 desvio = Random.insideUnitCircle * scatterRadius;
            Vector3 alvo = pe + new Vector3(desvio.x, 0f, desvio.y);

            // Prendo o ponto no navmesh: o desvio pode cair dentro de uma
            // parede, e pocao fora do navmesh o jogador nao alcanca. Sem
            // navmesh perto, prefiro nao soltar a soltar onde ninguem pega.
            if (!NavMesh.SamplePosition(alvo, out NavMeshHit chao, 1f, NavMesh.AllAreas))
                continue;

            // Sem pai de proposito: o inimigo some 2 s depois de morrer (o
            // destroyDelay do Health) e levaria a pocao junto.
            Instantiate(drop.prefab, chao.position, Quaternion.identity);
        }
    }
}
