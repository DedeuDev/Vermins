using System.Collections;
using UnityEngine;

/// <summary>
/// Script do Ian na sala do boss (EndRoom).
/// Mantem a escada de saida desligada ate o boss da sala morrer.
/// Quando o boss morre, o evento OnBossMorto liga a escada para levar o jogador a HUBCity.
///
/// O boss nasce pelo BossSpawner apos o RuntimeDungeonNavMesh terminar o bake, entao
/// espero o navmesh ficar pronto e mais um frame. Achei o BossStats uma unica vez na carga,
/// sem nenhum Find no Update.
/// </summary>
public class BossRoomExit : MonoBehaviour
{
    [Tooltip("Instancia da EscadaSaidaBoss nesta sala do boss.")]
    [SerializeField] private GameObject exitLadder;

    private BossStats boundBoss;
    private Health boundHealth;
    private bool activated;

    public GameObject ExitLadder => exitLadder;
    public bool IsActivated => activated;

    private void Awake()
    {
        if (exitLadder != null)
            exitLadder.SetActive(false);
    }

    private IEnumerator Start()
    {
        if (exitLadder != null)
            exitLadder.SetActive(false);

        // O boss nasce apos o RuntimeDungeonNavMesh terminar o bake.
        // Espero o navmesh e mais um frame para o BossSpawner instanciar o boss.
        RuntimeDungeonNavMesh navMesh = FindFirstObjectByType<RuntimeDungeonNavMesh>();
        if (navMesh != null)
        {
            int maxFrames = 300;
            int frames = 0;
            while (!navMesh.HasBuiltNavMesh && frames < maxFrames)
            {
                frames++;
                yield return null;
            }

            // Espera o frame do BossSpawner instanciar o boss
            yield return null;
        }

        // Procura o BossStats uma unica vez apos a geracao, sem busca por frame
        int attempts = 0;
        while (boundBoss == null && attempts < 15)
        {
            boundBoss = FindFirstObjectByType<BossStats>();
            if (boundBoss != null)
                break;

            attempts++;
            yield return null;
        }

        if (boundBoss != null)
        {
            boundBoss.OnBossMorto += HandleBossMorto;
            boundHealth = boundBoss.GetComponent<Health>();
            if (boundHealth != null)
                boundHealth.OnDied += HandleHealthDied;
        }
        else
        {
            Debug.LogWarning("BossRoomExit: nenhum BossStats encontrado na cena.", this);
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Unbind()
    {
        if (boundBoss != null)
        {
            boundBoss.OnBossMorto -= HandleBossMorto;
            boundBoss = null;
        }

        if (boundHealth != null)
        {
            boundHealth.OnDied -= HandleHealthDied;
            boundHealth = null;
        }
    }

    private void HandleHealthDied(Health _)
    {
        HandleBossMorto();
    }

    private void HandleBossMorto()
    {
        if (activated)
            return;

        activated = true;

        if (exitLadder != null)
        {
            exitLadder.SetActive(true);
        }
    }
}
