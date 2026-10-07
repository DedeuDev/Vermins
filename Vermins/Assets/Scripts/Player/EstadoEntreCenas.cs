using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Leva vida, pocoes, ouro e joias do Player de uma cena pra outra.
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
/// So quem chama o Guardar e a passagem entre cenas. Morrer, comecar
/// jogo novo e carregar o save nao passam por aqui de proposito: quem
/// morre renasce do zero, e o load do save aplica o que esta no arquivo.
/// </summary>
public static class EstadoEntreCenas
{
    private static bool temEstado;
    private static float vida;
    private static int pocoes;
    private static int ouro;
    private static int joias;
    private static int valorDasJoias;

    // Campo estatico nao zera sozinho quando o editor entra em Play sem
    // recarregar o dominio, e a inscricao no sceneLoaded dobraria. Por
    // isso limpo e inscrevo de novo aqui.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Ligar()
    {
        temEstado = false;

        SceneManager.sceneLoaded -= AoAbrirCena;
        SceneManager.sceneLoaded += AoAbrirCena;
    }

    /// <summary>
    /// Anota como o Player esta agora. Chamar logo antes do LoadScene da
    /// passagem.
    /// </summary>
    public static void Guardar()
    {
        temEstado = false;

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

        vida = saude.Current;
        pocoes = cinto != null ? cinto.Count : 0;
        ouro = carteira != null ? carteira.Amount : 0;
        joias = bolsa != null ? bolsa.Count : 0;
        valorDasJoias = bolsa != null ? bolsa.TotalValue : 0;

        temEstado = true;
    }

    private static void AoAbrirCena(Scene cena, LoadSceneMode modo)
    {
        if (!temEstado)
            return;

        // Vale uma vez so. Se a cena que abriu nao tiver Player, o estado
        // se perde em vez de aparecer numa cena qualquer mais tarde.
        temEstado = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        var saude = player.GetComponent<Health>();

        if (saude == null)
            return;

        // Classe estatica nao roda corrotina, entao pego o Health
        // emprestado pra rodar a espera.
        saude.StartCoroutine(Devolver(player, saude));
    }

    // O sceneLoaded chega depois dos Awake e antes dos Start. O
    // AtributosDoPersonagem aplica a build no Start e enche a vida, entao
    // se eu devolvesse aqui mesmo ele passaria por cima. Espero um frame.
    private static IEnumerator Devolver(GameObject player, Health saude)
    {
        yield return null;

        if (player == null)
            yield break;

        saude.Revive(vida);

        var cinto = player.GetComponent<PotionBelt>();

        if (cinto != null)
            cinto.Restore(pocoes);

        var carteira = player.GetComponent<GoldWallet>();

        if (carteira != null)
            carteira.Restore(ouro);

        var bolsa = player.GetComponent<JewelPouch>();

        if (bolsa != null)
            bolsa.Restore(joias, valorDasJoias);
    }
}
