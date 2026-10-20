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

        // Ian: o inventario do Player. Sem isto o load devolvia a vida mas
        // zerava pocao, ouro e joia. Componente que faltar fica zerado no
        // save, sem travar o resto.
        PotionBelt belt = player.GetComponent<PotionBelt>();

        if (belt != null)
        {
            data.playerPotions = belt.Count;
        }

        GoldWallet wallet = player.GetComponent<GoldWallet>();

        if (wallet != null)
        {
            data.playerGold = wallet.Amount;
        }

        JewelPouch pouch = player.GetComponent<JewelPouch>();

        if (pouch != null)
        {
            data.playerJewels = pouch.Count;
            data.playerJewelsValue = pouch.TotalValue;
        }

        // Ian: o XP do Player e os pontos gastos. Gravo so o XP total: o
        // nivel sai dele, e os pontos livres saem do nivel.
        CharacterLevel nivel = player.GetComponent<CharacterLevel>();

        if (nivel != null)
        {
            data.playerXp = nivel.Xp;
            data.playerPowerPoints = nivel.SpentPoints.Power;
            data.playerHastePoints = nivel.SpentPoints.Haste;
            data.playerVitalityPoints = nivel.SpentPoints.Vitality;
        }

        // Ian: o equipamento vestido, pelo nome do asset.
        CharacterEquipment equipamento = player.GetComponent<CharacterEquipment>();

        if (equipamento != null)
        {
            data.playerArma = EquipmentItem.SaveName(equipamento.GetItem(EquipmentSlot.Weapon));
            data.playerArmadura = EquipmentItem.SaveName(equipamento.GetItem(EquipmentSlot.Armor));
            data.playerAnel = EquipmentItem.SaveName(equipamento.GetItem(EquipmentSlot.Ring));
        }

        //Salvar os itens
        InventorySystem inventory = player.GetComponent<InventorySystem>();

        if (inventory != null)
        {
         data.playerInventoryItems = inventory.GetSavedItemNames();
        }
        
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

                    // Ian: o XP e os pontos gastos voltam antes da vida.
                    // O Revive corta a vida no teto, e o teto depende do
                    // nivel e da Vitalidade: com o nivel 1 ainda valendo,
                    // uma vida salva de 150 voltaria 100.
                    CharacterLevel nivel = player.GetComponent<CharacterLevel>();

                    if (nivel != null)
                    {
                        nivel.Restore(
                            pendingLoadData.playerXp,
                            new BonusPoints(
                                pendingLoadData.playerPowerPoints,
                                pendingLoadData.playerHastePoints,
                                pendingLoadData.playerVitalityPoints
                            )
                        );
                    }

                    // Ian: o equipamento tambem volta antes da vida: a
                    // Vitalidade do item sobe o teto, e sem ela a vida
                    // salva seria cortada. Nome que nao existir mais vira
                    // slot vazio, com aviso.
                    CharacterEquipment equipamento = player.GetComponent<CharacterEquipment>();

                    if (equipamento != null)
                    {
                        equipamento.Restore(
                            EquipmentItem.FindByName(pendingLoadData.playerArma),
                            EquipmentItem.FindByName(pendingLoadData.playerArmadura),
                            EquipmentItem.FindByName(pendingLoadData.playerAnel)
                        );
                    }

                    //Carregar os itens
                    InventorySystem inventory = player.GetComponent<InventorySystem>();

                    if (inventory != null)
                   {
                    inventory.RestoreItems(pendingLoadData.playerInventoryItems);
                   }

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

                    // Ian: devolve o inventario. Tem que ser aqui, depois
                    // que a cena abriu: o Player nasce de novo no reload e
                    // o Awake de cada componente volta ao valor inicial.
                    PotionBelt belt = player.GetComponent<PotionBelt>();

                    if (belt != null)
                    {
                        belt.Restore(pendingLoadData.playerPotions);
                    }

                    GoldWallet wallet = player.GetComponent<GoldWallet>();

                    if (wallet != null)
                    {
                        wallet.Restore(pendingLoadData.playerGold);
                    }

                    JewelPouch pouch = player.GetComponent<JewelPouch>();

                    if (pouch != null)
                    {
                        pouch.Restore(
                            pendingLoadData.playerJewels,
                            pendingLoadData.playerJewelsValue
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