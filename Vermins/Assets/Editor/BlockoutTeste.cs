using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta um pedaco de esgoto de mentira pra testar o player.
///
/// Andar num plano vazio nao testa quase nada. Aqui tem de proposito uma
/// coisa de cada tipo que o jogo vai ter: corredor apertado, sala grande
/// pra briga, poco de agua que nao da pra pisar, rampa, plataforma alta,
/// beco sem saida e uma sala de boss no fim. Cada um quebra o player de
/// um jeito diferente.
///
/// As medidas seguem o kit modular que ja esta em Placeholders/Modulos:
/// parede de 3,2 m e corredor de 4 m de largura. Assim o que der certo
/// aqui continua dando certo na dungeon de verdade.
///
/// Menu: Vermins > Cenario > Gerar blockout de teste
/// </summary>
public static class BlockoutTeste
{
    private const string PastaRaiz = "Assets/Placeholders/Blockout";
    private const string PastaMateriais = PastaRaiz + "/Materiais";
    private const string CaminhoPrefab = PastaRaiz + "/BlockoutTeste.prefab";

    private const float AlturaParede = 3.2f;

    // Parede que corre leste-oeste fica entre voce e a camera, e a
    // camera olha de 55 graus: uma parede de altura h esconde 0,70*h
    // metros de chao atras dela. Com 3,2 sao 2,2 m - o jogador some
    // atras dela num corredor de 4 m. Entao essas viram mureta.
    //
    // Isso e remendo de blockout. Na dungeon de verdade, com o kit
    // modular de parede inteira, a solucao e sumir com a parede que
    // estiver entre a camera e o jogador - e o que Diablo e PoE fazem.
    private const float AlturaMureta = 1.2f;
    private const float Espessura = 0.5f;
    private const float EspessuraPiso = 1.5f;
    private const float AlturaPlataforma = 2.5f;
    private const float FundoDaAgua = -1.2f;

    // O clique de andar so aceita a layer do chao, entao piso vai na
    // "ground" e o resto fica de fora. Clicar numa parede ou na agua
    // nao manda o jogador pra lugar nenhum, que e o certo.
    private const int LayerChao = 8;
    private const int LayerObstaculo = 3;
    private const int LayerAgua = 4;

    private static Transform grupo;

    [MenuItem("Vermins/Cenario/Gerar blockout de teste")]
    public static void Gerar()
    {
        GarantirPastas();

        Material pedra = Mat("Pedra", new Color32(0x6E, 0x69, 0x63, 0xFF));
        Material pisoCorredor = Mat("PisoCorredor", new Color32(0x45, 0x4C, 0x52, 0xFF));
        Material pisoArena = Mat("PisoArena", new Color32(0x4C, 0x52, 0x48, 0xFF));
        Material pisoBeco = Mat("PisoBeco", new Color32(0x3A, 0x37, 0x34, 0xFF));
        Material pisoAlto = Mat("PisoAlto", new Color32(0x6A, 0x62, 0x58, 0xFF));
        Material pisoCulto = Mat("PisoCulto", new Color32(0x44, 0x39, 0x52, 0xFF));
        Material pisoSeguro = Mat("PisoSeguro", new Color32(0x8A, 0x73, 0x55, 0xFF));
        Material agua = Mat("AguaContaminada", new Color32(0x46, 0x78, 0x42, 0xFF), 0.85f);
        Material culto = Mat("Culto", new Color32(0x5B, 0x3E, 0x7A, 0xFF));
        Material madeira = Mat("Madeira", new Color32(0x6B, 0x4A, 0x2F, 0xFF));
        Material tocha = Mat("Tocha", new Color32(0xC8, 0x7A, 0x3A, 0xFF), 0.2f,
                             new Color(1.6f, 0.75f, 0.28f));

        var raiz = new GameObject("BlockoutTeste");

        // ---------- sala inicial: onde o jogador nasce ----------
        grupo = Sub(raiz, "SalaInicial");
        Piso("Piso", -7f, 7f, -30f, -18f, 0f, pisoSeguro);
        Parede("Sul", -7f, -30f, 7f, -30f, pedra);
        Parede("Oeste", -7f, -30f, -7f, -18f, pedra);
        Parede("Leste", 7f, -30f, 7f, -18f, pedra);
        Parede("NorteEsq", -7f, -18f, -2f, -18f, pedra);
        Parede("NorteDir", 2f, -18f, 7f, -18f, pedra);
        Tocha(-6.5f, -24f, tocha);
        Tocha(6.5f, -24f, tocha);

        // ---------- corredor apertado ----------
        // 4 m de largura, igual o kit modular. E aqui que da pra ver a
        // parede do sul tampando o jogador: a camera olha de 55 graus,
        // entao parede de 3,13 m a menos de 2,2 m atras dele esconde.
        grupo = Sub(raiz, "CorredorApertado");
        Piso("Piso", -2f, 2f, -18f, -8f, 0f, pisoCorredor);
        Parede("OesteSul", -2f, -18f, -2f, -16f, pedra);
        Parede("OesteNorte", -2f, -12f, -2f, -8f, pedra);
        Parede("Leste", 2f, -18f, 2f, -8f, pedra);
        Tocha(-1.6f, -13f, tocha);

        // ---------- beco sem saida ----------
        grupo = Sub(raiz, "BecoSemSaida");
        Piso("Piso", -10f, -2f, -16f, -12f, 0f, pisoBeco);
        Parede("Sul", -10f, -16f, -2f, -16f, pedra);
        Parede("Norte", -10f, -12f, -2f, -12f, pedra);
        Parede("Oeste", -10f, -16f, -10f, -12f, pedra);
        Caixa("Caixote1", new Vector3(-8.4f, 0.5f, -14.2f), new Vector3(1f, 1f, 1f), madeira, LayerObstaculo);
        Caixa("Caixote2", new Vector3(-7.3f, 0.4f, -13.4f), new Vector3(0.8f, 0.8f, 0.8f), madeira, LayerObstaculo);
        Caixa("Caixote3", new Vector3(-8.1f, 1.4f, -14.3f), new Vector3(0.8f, 0.8f, 0.8f), madeira, LayerObstaculo);

        // ---------- sala dos pilares: a arena de briga ----------
        // O poco no meio obriga a contornar. Serve pra ver o caminho do
        // NavMesh e pra testar clique em cima de coisa que nao da pra
        // pisar - a agua nao esta na layer do chao de proposito.
        grupo = Sub(raiz, "SalaDosPilares");
        Piso("PisoOeste", -10f, -5f, -8f, 8f, 0f, pisoArena);
        Piso("PisoLeste", 3f, 10f, -8f, 8f, 0f, pisoArena);
        Piso("PisoSul", -5f, 3f, -8f, -2.5f, 0f, pisoArena);
        Piso("PisoNorte", -5f, 3f, 2.5f, 8f, 0f, pisoArena);
        Agua("Agua", -5f, 3f, -2.5f, 2.5f, agua);
        Parede("SulEsq", -10f, -8f, -2f, -8f, pedra);
        Parede("SulDir", 2f, -8f, 10f, -8f, pedra);
        Parede("NorteEsq", -10f, 8f, -2f, 8f, pedra);
        Parede("NorteDir", 2f, 8f, 10f, 8f, pedra);
        Parede("Oeste", -10f, -8f, -10f, 8f, pedra);
        Parede("LesteSul", 10f, -8f, 10f, -2f, pedra);
        Parede("LesteNorte", 10f, 2f, 10f, 8f, pedra);
        Pilar("Pilar1", -8f, -6f, pedra);
        Pilar("Pilar2", -8f, 6f, pedra);
        Pilar("Pilar3", 8f, -6f, pedra);
        Pilar("Pilar4", 8f, 6f, pedra);
        Tocha(-9.5f, -4f, tocha);
        Tocha(-9.5f, 4f, tocha);

        // ---------- rampa e plataforma alta ----------
        // 14 graus, bem dentro dos 45 que o NavMesh aceita. Serve pra
        // ver a animacao em ladeira e pra o jogador ter um lugar de onde
        // olhar a arena de cima.
        grupo = Sub(raiz, "RampaEPlataforma");
        Rampa("Rampa", 10f, 20f, 0f, AlturaPlataforma, 4f, pisoAlto);
        Piso("Plataforma", 20f, 30f, -5f, 5f, AlturaPlataforma, pisoAlto);
        Parapeito("ParapeitoSul", 20f, -5f, 30f, -5f, pedra);
        Parapeito("ParapeitoNorte", 20f, 5f, 30f, 5f, pedra);
        Parapeito("ParapeitoLeste", 30f, -5f, 30f, 5f, pedra);
        Parapeito("ParapeitoOesteSul", 20f, -5f, 20f, -2f, pedra);
        Parapeito("ParapeitoOesteNorte", 20f, 2f, 20f, 5f, pedra);

        // ---------- corredor pro culto ----------
        grupo = Sub(raiz, "CorredorNorte");
        Piso("Piso", -2f, 2f, 8f, 17f, 0f, pisoCorredor);
        Parede("Oeste", -2f, 8f, -2f, 17f, pedra);
        Parede("Leste", 2f, 8f, 2f, 17f, pedra);
        Tocha(-1.6f, 13f, tocha);

        // ---------- sala do culto: o lugar do boss ----------
        grupo = Sub(raiz, "SalaDoCulto");
        Piso("Piso", -8f, 8f, 17f, 31f, 0f, pisoCulto);
        Parede("SulEsq", -8f, 17f, -2f, 17f, culto);
        Parede("SulDir", 2f, 17f, 8f, 17f, culto);
        Parede("Oeste", -8f, 17f, -8f, 31f, culto);
        Parede("Leste", 8f, 17f, 8f, 31f, culto);
        Parede("Norte", -8f, 31f, 8f, 31f, culto);
        Caixa("Altar", new Vector3(0f, 0.6f, 25f), new Vector3(3f, 1.2f, 3f), culto, LayerObstaculo);
        Pilar("Pilar1", -5f, 20f, culto);
        Pilar("Pilar2", 5f, 20f, culto);
        Pilar("Pilar3", -5f, 29f, culto);
        Pilar("Pilar4", 5f, 29f, culto);
        Tocha(-7.5f, 24f, tocha);
        Tocha(7.5f, 24f, tocha);

        var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, CaminhoPrefab);
        Object.DestroyImmediate(raiz);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Blockout] Gerado em {CaminhoPrefab} com " +
                  $"{prefab.GetComponentsInChildren<Renderer>().Length} pecas. " +
                  "Arraste na cena e mande assar o NavMesh.", prefab);
    }

    // ----------------------------------------------------------------

    private static Transform Sub(GameObject raiz, string nome)
    {
        var g = new GameObject(nome);
        g.transform.SetParent(raiz.transform, false);
        return g.transform;
    }

    private static GameObject Caixa(string nome, Vector3 centro, Vector3 tamanho,
                                    Material mat, int layer, Quaternion? giro = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = nome;
        g.layer = layer;
        g.transform.SetParent(grupo, false);
        g.transform.localPosition = centro;
        g.transform.localScale = tamanho;

        if (giro.HasValue)
            g.transform.localRotation = giro.Value;

        g.GetComponent<MeshRenderer>().sharedMaterial = mat;

        return g;
    }

    /// <summary>Laje de chao com a face de cima na altura pedida.</summary>
    private static void Piso(string nome, float x0, float x1, float z0, float z1,
                             float topo, Material mat)
    {
        Caixa(nome,
              new Vector3((x0 + x1) * 0.5f, topo - EspessuraPiso * 0.5f, (z0 + z1) * 0.5f),
              new Vector3(x1 - x0, EspessuraPiso, z1 - z0),
              mat, LayerChao);
    }

    /// <summary>
    /// Agua parada no fundo do poco. Fica fora da layer do chao pra o
    /// clique nao aceitar, e marcada como nao-andavel pra o NavMesh nao
    /// tentar assar uma ilha la embaixo.
    /// </summary>
    private static void Agua(string nome, float x0, float x1, float z0, float z1, Material mat)
    {
        var g = Caixa(nome,
                      new Vector3((x0 + x1) * 0.5f, FundoDaAgua - 0.25f, (z0 + z1) * 0.5f),
                      new Vector3(x1 - x0, 0.5f, z1 - z0),
                      mat, LayerAgua);

        var mod = g.AddComponent<NavMeshModifier>();
        mod.overrideArea = true;
        mod.area = 1; // Not Walkable
    }

    /// <summary>
    /// Parede alta se corre norte-sul, mureta se corre leste-oeste.
    /// Nos dois casos o NavMeshAgent nao passa: o degrau que ele sobe e
    /// 0,75 m e a mureta tem 1,2.
    /// </summary>
    private static void Parede(string nome, float x0, float z0, float x1, float z1, Material mat)
    {
        bool lesteOeste = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);
        Muro(nome, x0, z0, x1, z1, 0f, lesteOeste ? AlturaMureta : AlturaParede, mat);
    }

    /// <summary>Mureta baixa na beirada da plataforma, pra nao tampar a vista.</summary>
    private static void Parapeito(string nome, float x0, float z0, float x1, float z1, Material mat)
    {
        Muro(nome, x0, z0, x1, z1, AlturaPlataforma, 0.9f, mat);
    }

    private static void Muro(string nome, float x0, float z0, float x1, float z1,
                             float baseY, float altura, Material mat)
    {
        var a = new Vector3(x0, 0f, z0);
        var b = new Vector3(x1, 0f, z1);

        // Somo a espessura no comprimento pra as quinas fecharem sem
        // fresta - duas paredes que se encontram invadem uma a outra.
        float comprimento = Vector3.Distance(a, b) + Espessura;
        float anguloY = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;

        Caixa(nome,
              (a + b) * 0.5f + Vector3.up * (baseY + altura * 0.5f),
              new Vector3(Espessura, altura, comprimento),
              mat, LayerObstaculo,
              Quaternion.Euler(0f, anguloY, 0f));
    }

    private static void Pilar(string nome, float x, float z, Material mat)
    {
        Caixa(nome, new Vector3(x, AlturaParede * 0.5f, z),
              new Vector3(1.5f, AlturaParede, 1.5f), mat, LayerObstaculo);
    }

    private static void Tocha(float x, float z, Material mat)
    {
        Caixa("Tocha", new Vector3(x, 2f, z), new Vector3(0.35f, 0.7f, 0.35f),
              mat, LayerObstaculo);
    }

    /// <summary>
    /// Rampa entre duas alturas. Calculo o centro descontando meia
    /// espessura na normal da laje, senao a face de cima nao encosta no
    /// piso de baixo nem na plataforma de cima.
    /// </summary>
    private static void Rampa(string nome, float x0, float x1, float y0, float y1,
                              float largura, Material mat)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float comprimento = Mathf.Sqrt(dx * dx + dy * dy);
        float angulo = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

        var meioDoTopo = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
        var normal = Quaternion.Euler(0f, 0f, angulo) * Vector3.up;

        Caixa(nome,
              meioDoTopo - normal * (Espessura * 0.5f),
              // Estico um pouco pra cravar nas duas pontas.
              new Vector3(comprimento + 1f, Espessura, largura),
              mat, LayerChao,
              Quaternion.Euler(0f, 0f, angulo));
    }

    private static Material Mat(string nome, Color cor, float brilho = 0.15f,
                                Color? emissao = null)
    {
        string caminho = $"{PastaMateriais}/{nome}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);

        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, caminho);
        }

        mat.SetColor("_BaseColor", cor);
        mat.SetFloat("_Smoothness", brilho);

        if (emissao.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emissao.Value);
        }

        EditorUtility.SetDirty(mat);

        return mat;
    }

    private static void GarantirPastas()
    {
        if (!AssetDatabase.IsValidFolder(PastaRaiz))
            AssetDatabase.CreateFolder("Assets/Placeholders", "Blockout");

        if (!AssetDatabase.IsValidFolder(PastaMateriais))
            AssetDatabase.CreateFolder(PastaRaiz, "Materiais");
    }
}
