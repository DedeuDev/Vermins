using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arquivo do Ian. Poe um pilar em cada lado de cada porta da dungeon pra
/// esconder a emenda entre os modulos.
///
/// Medi na DungeonComPlayer: o corredor tem 3,5 m por dentro (parede a
/// 1,75 m do centro da porta) e a abertura da sala tem 4 m (batente a
/// 2 m). A parede da sala ainda fica 0,25 m pra dentro da borda do
/// modulo, e a do corredor fica rente. Na emenda sobrava um degrau de
/// 0,25 m com uma fresta, que parecia parede quebrada. O pilar cobre de
/// 1,6 m a 2,3 m pro lado e 0,4 m pra cada lado da borda: tampa o degrau
/// e vira batente, que e o truque de kit modular. Ele e um pouco mais
/// alto que a parede: a camera ve muito o topo, e o degrau aparecia ali.
///
/// Nao mexe no gerador nem nas malhas do Eduardo: le os DungeonSocket que
/// ele deixou nos filhos. Porta encaixada tem dois sockets no mesmo lugar
/// (um de cada modulo), entao guardo a posicao pra nao por pilar duplo.
///
/// O pilar nao tem collider e nao fecha caminho: a face de dentro fica a
/// 1,6 m do centro, e o NavMesh do corredor para em 1,25 m (parede a 1,75
/// menos o raio do agente, 0,5).
///
/// Roda uma vez, quando o NavMesh fica pronto, como o DeadGuardSpawner.
/// </summary>
[RequireComponent(typeof(RuntimeDungeonNavMesh))]
public class DungeonSeams : MonoBehaviour
{
    [Tooltip("O PilarDaPorta.prefab: bloco com pivo no chao.")]
    [SerializeField] private GameObject pillarPrefab;

    [Tooltip("Do centro da porta ate o centro do pilar, pro lado. 1,95 " +
             "cobre o batente da sala (2 m) e a parede do corredor (1,75 m).")]
    [SerializeField, Min(0f)] private float jambOffset = 1.95f;

    [Tooltip("Maximo de frames esperando o NavMesh, igual aos spawners.")]
    [SerializeField, Min(1)] private int maxFramesWaitingForNavMesh = 300;

    private RuntimeDungeonNavMesh runtimeNavMesh;
    private Transform generatedRoot;

    /// <summary>Quantos pilares foram postos. O teste le daqui.</summary>
    public int PillarCount { get; private set; }

    private void Awake()
    {
        runtimeNavMesh = GetComponent<RuntimeDungeonNavMesh>();
    }

    private void Start()
    {
        StartCoroutine(BuildWhenReady());
    }

    private IEnumerator BuildWhenReady()
    {
        int esperou = 0;

        while (!runtimeNavMesh.HasBuiltNavMesh && esperou < maxFramesWaitingForNavMesh)
        {
            esperou++;
            yield return null;
        }

        if (!runtimeNavMesh.HasBuiltNavMesh)
        {
            Debug.LogError("DungeonSeams: o NavMesh nao ficou pronto.", this);
            yield break;
        }

        Build();
    }

    [ContextMenu("Rebuild Seams")]
    private void Build()
    {
        if (generatedRoot != null)
        {
            // Solto antes do Destroy, que so vale no fim do frame.
            generatedRoot.SetParent(null);
            Destroy(generatedRoot.gameObject);
        }

        PillarCount = 0;

        if (pillarPrefab == null)
            return;

        generatedRoot = new GameObject("Generated Seams").transform;
        generatedRoot.SetParent(transform, false);

        // Porta encaixada aparece duas vezes, uma por modulo. Arredondo a
        // posicao em 10 cm pra reconhecer a mesma porta.
        HashSet<Vector3Int> portas = new HashSet<Vector3Int>();

        foreach (DungeonSocket socket in GetComponentsInChildren<DungeonSocket>())
        {
            Vector3 centro = socket.transform.position;

            if (!portas.Add(Vector3Int.RoundToInt(centro * 10f)))
                continue;

            Vector3 frente = socket.transform.forward;
            frente.y = 0f;

            if (frente.sqrMagnitude < 0.01f)
                continue;

            frente.Normalize();
            Vector3 lado = Vector3.Cross(Vector3.up, frente);
            Quaternion giro = Quaternion.LookRotation(frente);

            Instantiate(pillarPrefab, centro + lado * jambOffset, giro, generatedRoot);
            Instantiate(pillarPrefab, centro - lado * jambOffset, giro, generatedRoot);
            PillarCount += 2;
        }
    }
}
