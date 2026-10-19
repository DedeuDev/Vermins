using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Solta itens no chao quando o dono morre.
///
/// Vai em qualquer coisa que tenha Health - hoje o inimigo, depois
/// barril ou chefe - e nao mexe na IA de ninguem: so escuta o OnDied,
/// do mesmo jeito que o DamageFlash escuta o OnDamaged. Por isso o
/// EnemyFollow do Leo nao precisou mudar.
///
/// Cada item da lista sorteia sozinho. Quando o ouro existir, ele entra
/// na lista do lado da pocao, e os dois podem cair na mesma morte.
///
/// O equipamento sorteia separado, em duas etapas: primeiro a raridade
/// pelo peso, depois um item dessa raridade na lista. Assim a chance de
/// raro nao depende de quantos itens raros existem na lista.
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

    [Header("Equipamento")]
    [Tooltip("Chance de cair um equipamento a cada morte, de 0 a 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float chanceDeEquipamento = 0.15f;

    [Tooltip("O prefab do item no chao. O item sorteado e passado pra ele.")]
    [SerializeField] private EquipmentPickup prefabDeEquipamento;

    [Tooltip("Os itens que podem cair. A raridade de cada um vem do asset.")]
    [SerializeField] private EquipmentItem[] equipamentos;

    // Pesos, e nao porcentagens, pra ninguem precisar fechar 100 ao mexer
    // num so. Com 70, 25 e 5 a soma e 100 e o peso ja e a porcentagem.
    [Tooltip("Peso do comum no sorteio da raridade.")]
    [SerializeField, Min(0)] private int pesoComum = 70;

    [Tooltip("Peso do magico no sorteio da raridade.")]
    [SerializeField, Min(0)] private int pesoMagico = 25;

    [Tooltip("Peso do raro no sorteio da raridade.")]
    [SerializeField, Min(0)] private int pesoRaro = 5;

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

            if (!AcharChao(pe, out Vector3 ponto))
                continue;

            // Sem pai de proposito: o inimigo some 2 s depois de morrer (o
            // destroyDelay do Health) e levaria a pocao junto.
            Instantiate(drop.prefab, ponto, Quaternion.identity);
        }

        SoltarEquipamento(pe);
    }

    private void SoltarEquipamento(Vector3 pe)
    {
        if (prefabDeEquipamento == null || equipamentos == null || equipamentos.Length == 0)
            return;

        if (chanceDeEquipamento <= 0f || Random.value > chanceDeEquipamento)
            return;

        EquipmentItem item = SortearItem(SortearRaridade());

        if (item == null || !AcharChao(pe, out Vector3 ponto))
            return;

        // O Definir vem antes do Start do Pickup, que so roda no frame
        // seguinte, entao o rotulo ja nasce com o nome e a cor do item.
        EquipmentPickup noChao = Instantiate(prefabDeEquipamento, ponto, Quaternion.identity);
        noChao.Definir(item);
    }

    private Rarity SortearRaridade()
    {
        int total = pesoComum + pesoMagico + pesoRaro;

        if (total <= 0)
            return Rarity.Comum;

        // O Range de int nao inclui o maximo: sai de 0 a total - 1.
        int sorteio = Random.Range(0, total);

        if (sorteio < pesoComum)
            return Rarity.Comum;

        if (sorteio < pesoComum + pesoMagico)
            return Rarity.Magico;

        return Rarity.Raro;
    }

    /// <summary>
    /// Um item da raridade pedida. Sem item dela na lista, desce pra de
    /// baixo: um raro que nao existe vira magico, e nao "nada". Se nem o
    /// comum existir, sorteia qualquer um da lista.
    /// </summary>
    private EquipmentItem SortearItem(Rarity raridade)
    {
        for (int r = (int)raridade; r >= 0; r--)
        {
            // Duas voltas na lista, contando e depois escolhendo, pra nao
            // montar lista nova a cada morte.
            int quantos = 0;
            foreach (EquipmentItem item in equipamentos)
            {
                if (item != null && (int)item.Rarity == r)
                    quantos++;
            }

            if (quantos == 0)
                continue;

            int escolhido = Random.Range(0, quantos);
            foreach (EquipmentItem item in equipamentos)
            {
                if (item == null || (int)item.Rarity != r)
                    continue;

                if (escolhido == 0)
                    return item;

                escolhido--;
            }
        }

        return equipamentos[Random.Range(0, equipamentos.Length)];
    }

    private bool AcharChao(Vector3 pe, out Vector3 ponto)
    {
        Vector2 desvio = Random.insideUnitCircle * scatterRadius;
        Vector3 alvo = pe + new Vector3(desvio.x, 0f, desvio.y);

        // Prendo o ponto no navmesh: o desvio pode cair dentro de uma
        // parede, e item fora do navmesh o jogador nao alcanca. Sem
        // navmesh perto, prefiro nao soltar a soltar onde ninguem pega.
        if (NavMesh.SamplePosition(alvo, out NavMeshHit chao, 1f, NavMesh.AllAreas))
        {
            ponto = chao.position;
            return true;
        }

        ponto = default;
        return false;
    }
}
