using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Leva vida, XP, pontos gastos, equipamento, pocoes, ouro e joias do Player de uma cena
/// pra outra.
///
/// Cada cena tem o seu proprio Player, e o Awake de cada componente
/// volta ao valor inicial. Medi em Play: sai da cidade com vida 40, 3
/// pocoes, 37 de ouro e 2 joias e cheguei na dungeon com 100, 0, 0 e 0.
/// Na volta pela escada, a mesma coisa. Entrar e sair do bueiro virava
/// cura de graca, e o ouro pego na dungeon nunca chegava na cidade.
///
/// Guardo numa memoria estatica em vez de fazer o Player sobreviver a
/// troca de cena. Com DontDestroyOnLoad eu teria dois Players na cena
/// nova (o que veio e o que ja mora la) e teria que apagar um.
///
/// So quem chama o Capture e a passagem entre cenas. Morrer, comecar
/// jogo novo e carregar o save nao passam por aqui de proposito: quem
/// morre renasce do zero, e o load do save aplica o que esta no arquivo.
/// A excecao e o XP da morte, que o CharacterLevel cuida sozinho.
///
/// O equipamento vai como referencia ao asset, e nao pelo nome como no
/// save: a memoria estatica nao sai do jogo, entao nao precisa virar texto.
/// </summary>
public static class CrossSceneState
{
    private static bool hasState;
    private static float health;
    private static int xp;
    private static BonusPoints spentPoints;
    private static int potions;
    private static int gold;
    private static int jewels;
    private static int jewelsValue;
    private static EquipmentItem weapon;
    private static EquipmentItem armor;
    private static EquipmentItem ring;

    // Campo estatico nao zera sozinho quando o editor entra em Play sem
    // recarregar o dominio, e a inscricao no sceneLoaded dobraria. Por
    // isso limpo e inscrevo de novo aqui.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        hasState = false;
        weapon = null;
        armor = null;
        ring = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>
    /// Anota como o Player esta agora. Chamar logo antes do LoadScene da
    /// passagem.
    /// </summary>
    public static void Capture()
    {
        hasState = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        var saude = player.GetComponent<Health>();

        // Morto nao leva estado: o Revive com zero encheria a vida, e
        // atravessar a passagem morrendo viraria cura.
        if (saude == null || saude.IsDead)
            return;

        var cinto = player.GetComponent<PotionBelt>();
        var carteira = player.GetComponent<GoldWallet>();
        var bolsa = player.GetComponent<JewelPouch>();
        var nivel = player.GetComponent<CharacterLevel>();
        var equipamento = player.GetComponent<CharacterEquipment>();

        health = saude.Current;
        xp = nivel != null ? nivel.Xp : 0;
        spentPoints = nivel != null ? nivel.SpentPoints : default;
        potions = cinto != null ? cinto.Count : 0;
        gold = carteira != null ? carteira.Amount : 0;
        jewels = bolsa != null ? bolsa.Count : 0;
        jewelsValue = bolsa != null ? bolsa.TotalValue : 0;
        weapon = equipamento != null ? equipamento.GetItem(EquipmentSlot.Weapon) : null;
        armor = equipamento != null ? equipamento.GetItem(EquipmentSlot.Armor) : null;
        ring = equipamento != null ? equipamento.GetItem(EquipmentSlot.Ring) : null;

        hasState = true;
    }

    private static void HandleSceneLoaded(Scene cena, LoadSceneMode modo)
    {
        if (!hasState)
            return;

        // Vale uma vez so. Se a cena que abriu nao tiver Player, o estado
        // se perde em vez de aparecer numa cena qualquer mais tarde.
        hasState = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        var saude = player.GetComponent<Health>();

        if (saude == null)
            return;

        // Classe estatica nao roda corrotina, entao pego o Health
        // emprestado pra rodar a espera.
        saude.StartCoroutine(RestoreNextFrame(player, saude));
    }

    // O sceneLoaded chega depois dos Awake e antes dos Start. O
    // CharacterAttributes aplica a build no Start e enche a vida, entao
    // se eu devolvesse aqui mesmo ele passaria por cima. Espero um frame.
    private static IEnumerator RestoreNextFrame(GameObject player, Health saude)
    {
        yield return null;

        if (player == null)
            yield break;

        // O XP e os pontos gastos voltam antes da vida. O Revive corta a
        // vida no teto, e o teto depende do nivel e da Vitalidade: com o
        // nivel 1 ainda valendo, os 140 de vida de um nivel 6 voltariam 100.
        var nivel = player.GetComponent<CharacterLevel>();

        if (nivel != null)
            nivel.Restore(xp, spentPoints);

        // O equipamento tambem volta antes da vida, pelo mesmo motivo: a
        // Vitalidade do item sobe o teto. Com uma armadura de V+3, a vida
        // acima do teto sem ela seria cortada.
        var equipamento = player.GetComponent<CharacterEquipment>();

        if (equipamento != null)
            equipamento.Restore(weapon, armor, ring);

        saude.Revive(health);

        var cinto = player.GetComponent<PotionBelt>();

        if (cinto != null)
            cinto.Restore(potions);

        var carteira = player.GetComponent<GoldWallet>();

        if (carteira != null)
            carteira.Restore(gold);

        var bolsa = player.GetComponent<JewelPouch>();

        if (bolsa != null)
            bolsa.Restore(jewels, jewelsValue);
    }
}
