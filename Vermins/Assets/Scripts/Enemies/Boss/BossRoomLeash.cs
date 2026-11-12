using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ian: prende o boss na sala dele. A IA do Monge persegue o Player a
/// qualquer distancia e a do Ladino atira a 10 m mesmo com parede no meio,
/// entao o boss puxava o Player de fora da sala.
///
/// Com o Player fora da sala, desligo a IA (BossMonge, BossRogue ou
/// BossSacerdote) e o boss volta pro ponto onde nasceu. Com o Player
/// dentro, a IA liga. Levar dano tambem acorda por alguns segundos, pra
/// nao dar pra matar o boss parado da porta com a onda de choque.
///
/// Voltar nao cura: quem sai pra tomar pocao nao perde o dano que deu.
///
/// Quem passa a sala e o BossSpawner, logo depois de criar o boss. Sem
/// sala (Sandbox, cena de teste do Leonardo) o componente se desliga e o
/// boss fica como sempre foi.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class BossRoomLeash : MonoBehaviour
{
    [Tooltip("Metros a mais em volta da caixa da sala, pra porta contar " +
             "como dentro.")]
    [SerializeField] private float roomMargin = 1f;

    [Tooltip("Segundos que o boss fica acordado depois de levar dano com " +
             "o Player fora da sala.")]
    [SerializeField] private float wakeOnDamage = 5f;

    private NavMeshAgent agent;
    private Health health;
    private Transform player;
    private Behaviour[] brains;

    private bool hasRoom;
    private Bounds room;
    private Vector3 home;
    private Quaternion homeRotation;

    private bool engaged = true;
    private float awakeUntil = float.NegativeInfinity;

    /// <summary>Chamado pelo BossSpawner com a caixa da sala do boss.</summary>
    public void SetRoom(Bounds caixa)
    {
        caixa.Expand(new Vector3(roomMargin * 2f, 0f, roomMargin * 2f));
        room = caixa;
        hasRoom = true;
        home = transform.position;
        homeRotation = transform.rotation;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

        // So um dos tres existe em cada prefab; os nulos ficam de fora.
        brains = new Behaviour[]
        {
            GetComponent<BossMonge>(),
            GetComponent<BossRogue>(),
            GetComponent<BossSacerdote>(),
        };
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDamaged -= HandleDamaged;
    }

    private void Start()
    {
        if (!hasRoom)
        {
            enabled = false;
            return;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");

        if (p != null)
            player = p.transform;

        // Comeca dormindo: o Player nasce longe, na sala inicial.
        SetEngaged(false);
    }

    private void HandleDamaged(float dano, GameObject origem)
    {
        awakeUntil = Time.time + wakeOnDamage;
    }

    private void Update()
    {
        if (health != null && health.IsDead)
            return;

        bool quer = Time.time < awakeUntil || IsPlayerInRoom();

        if (quer != engaged)
            SetEngaged(quer);

        if (!engaged)
            ReturnHome();
    }

    // So o plano: a caixa da sala e baixa e o Player pula na escada.
    private bool IsPlayerInRoom()
    {
        if (player == null)
            return false;

        Vector3 pos = player.position;
        return pos.x >= room.min.x && pos.x <= room.max.x &&
               pos.z >= room.min.z && pos.z <= room.max.z;
    }

    private void SetEngaged(bool valor)
    {
        engaged = valor;

        for (int i = 0; i < brains.Length; i++)
        {
            if (brains[i] != null)
                brains[i].enabled = valor;
        }
    }

    private void ReturnHome()
    {
        // O boss nasce antes do NavMesh da dungeon ficar pronto.
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        // A IA desligada pode ter deixado o agente parado (o Monge para
        // no terremoto, o Ladino quando o Player se afasta).
        agent.isStopped = false;

        Vector3 delta = agent.destination - home;
        delta.y = 0f;

        if (delta.sqrMagnitude > 0.25f)
            agent.SetDestination(home);

        // Chegou: vira pra porta de novo, como quando nasceu.
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, 360f * Time.deltaTime);
    }
}
