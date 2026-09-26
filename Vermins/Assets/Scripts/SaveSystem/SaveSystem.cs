using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance;

    private string savePath;
    private SaveData pendingLoadData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        savePath = Path.Combine(
            Application.persistentDataPath,
            "save.json"
        );
    }

    public void SaveGame()
    {
        Debug.Log("SAVE FOI CHAMADO!");

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError("Player não encontrado.");
            return;
        }

        Health health = player.GetComponent<Health>();

        if (health == null)
        {
            Debug.LogError("Componente Health não encontrado no Player.");
            return;
        }

        SaveData data = new SaveData();

        data.sceneName = SceneManager.GetActiveScene().name;

        data.playerPosX = player.transform.position.x;
        data.playerPosY = player.transform.position.y;
        data.playerPosZ = player.transform.position.z;

        data.playerRotX = player.transform.eulerAngles.x;
        data.playerRotY = player.transform.eulerAngles.y;
        data.playerRotZ = player.transform.eulerAngles.z;

        data.playerHealth = health.Current;

        string json = JsonUtility.ToJson(data, true);

        File.WriteAllText(savePath, json);

        Debug.Log("Jogo salvo.");
        Debug.Log("Vida salva: " + data.playerHealth);
    }

    public void LoadGame()
    {
        Debug.Log("LOAD FOI CHAMADO!");

        if (!File.Exists(savePath))
        {
            Debug.Log("Nenhum save encontrado.");
            return;
        }

        string json = File.ReadAllText(savePath);

        pendingLoadData = JsonUtility.FromJson<SaveData>(json);

        Debug.Log("Vida encontrada no save: " + pendingLoadData.playerHealth);

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        SceneManager.LoadScene(pendingLoadData.sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        StartCoroutine(WaitForDungeonAndLoadPlayer());
    }

    private IEnumerator WaitForDungeonAndLoadPlayer()
    {
        float timeout = 30f;
        float timer = 0f;

        Debug.Log("Aguardando o dungeon e o NavMesh...");

        while (timer < timeout)
        {
            timer += Time.deltaTime;

            if (pendingLoadData == null)
            {
                yield break;
            }

            Vector3 savedPosition = new Vector3(
                pendingLoadData.playerPosX,
                pendingLoadData.playerPosY,
                pendingLoadData.playerPosZ
            );

            if (NavMesh.SamplePosition(
                savedPosition,
                out NavMeshHit navMeshHit,
                100f,
                NavMesh.AllAreas))
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");

                if (player != null)
                {
                    NavMeshAgent agent = player.GetComponent<NavMeshAgent>();

                    if (agent != null)
                    {
                        if (!agent.enabled)
                        {
                            yield return null;
                            continue;
                        }

                        if (!agent.isOnNavMesh)
                        {
                            yield return null;
                            continue;
                        }

                        agent.Warp(navMeshHit.position);
                    }
                    else
                    {
                        player.transform.position = navMeshHit.position;
                    }

                    player.transform.eulerAngles = new Vector3(
                        pendingLoadData.playerRotX,
                        pendingLoadData.playerRotY,
                        pendingLoadData.playerRotZ
                    );

                    Health health = player.GetComponent<Health>();

                    if (health != null)
                    {
                        health.Revive(pendingLoadData.playerHealth);

                        Debug.Log(
                            "Vida restaurada: " + health.Current
                        );
                    }
                    else
                    {
                        Debug.LogError(
                            "Componente Health não encontrado no Player."
                        );
                    }

                    Debug.Log("Jogo carregado com sucesso.");

                    pendingLoadData = null;

                    yield break;
                }
            }

            yield return null;
        }

        Debug.LogError(
            "Não foi possível carregar o jogador: NavMesh ou Player não encontrado."
        );

        pendingLoadData = null;
    }

    public bool HasSave()
    {
        return File.Exists(savePath);
    }
}