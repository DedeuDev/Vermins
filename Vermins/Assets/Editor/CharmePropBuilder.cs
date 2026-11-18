using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gera os materiais, malhas procedurais facetadas (low poly) e prefabs de
/// props do plano de charme da dungeon, alem da VigaDaPorta e do ajuste nos
/// DungeonPropSet. Rodar novamente regrava os mesmos arquivos mantendo GUID.
/// </summary>
public static class CharmePropBuilder
{
    private const string BlockoutRoot = "Assets/Placeholders/Blockout";
    private const string MatDir = BlockoutRoot + "/Materiais";
    private const string PropsDir = BlockoutRoot + "/Props";
    private const string MeshDir = PropsDir + "/Malhas";

    [MenuItem("Vermins/Cenario/Gerar props do charme")]
    public static void Gerar()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("CharmePropBuilder: saia do Play antes de gerar.");
            return;
        }

        EnsureFolders();

        // 1. Materiais (todos copiados de Madeira.mat -> Vermins/DungeonLit)
        Material madeira = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Madeira.mat");
        Material esgotosMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/DungeonEsgotos.mat");
        Material profMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/DungeonProfundezas.mat");
        Material cultoRef = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Culto.mat");
        Material pocaVerde = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PocaVerde.mat");

        Color corEsgotos = esgotosMat != null ? esgotosMat.GetColor("_BaseColor") : new Color(0.46f, 0.39f, 0.32f, 1f);
        Color corProf = profMat != null ? profMat.GetColor("_BaseColor") : new Color(0.25f, 0.31f, 0.25f, 1f);
        Color corCulto = cultoRef != null ? cultoRef.GetColor("_BaseColor") : new Color(0.357f, 0.243f, 0.478f, 1f);

        Material matOsso = EnsureMaterial(madeira, "Osso", new Color(0.72f, 0.67f, 0.56f, 1f), 0.20f, null);
        Material matFerro = EnsureMaterial(madeira, "Ferro", new Color(0.24f, 0.25f, 0.27f, 1f), 0.45f, null);
        Material matTecido = EnsureMaterial(madeira, "Tecido", new Color(0.54f, 0.45f, 0.33f, 1f), 0.12f, null);
        // Rodada 2: verde-oliva escuro e menos saturado. O verde de antes (0.28, 0.38, 0.21) virava tapete.
        Material matMusgo = EnsureMaterial(madeira, "Musgo", new Color(0.25f, 0.28f, 0.17f, 1f), 0.15f, null);
        Material matPedraDungeon = EnsureMaterial(madeira, "PedraDungeon", new Color(0.55f, 0.53f, 0.50f, 1f), 0.20f, null);
        Material matCristalRoxo = EnsureMaterial(madeira, "CristalRoxo", new Color(0.46f, 0.28f, 0.62f, 1f), 0.15f, new Color(0.015f, 0.006f, 0.028f, 1f));
        Material matSimboloCulto = EnsureMaterial(madeira, "SimboloCulto", corCulto, 0.10f, new Color(0.006f, 0.002f, 0.012f, 1f));

        // Rodada 2: a laje 15% mais clara virava adesivo branco. Agora fica com a luminancia
        // do piso (A = 1,00x e B = 0,95x, nunca acima de 1,05x), so puxada para cinza azulado,
        // e o rejunte escuro (valor 35% menor) e que desenha a grade.
        float lisoEsg = esgotosMat != null ? esgotosMat.GetFloat("_Smoothness") : 0.35f;
        Material matLajeEsgotos = EnsureMaterial(madeira, "LajeEsgotos", TomDaLaje(corEsgotos, 1.00f), lisoEsg, null);
        Material matLajeEsgotosB = EnsureMaterial(madeira, "LajeEsgotosB", TomDaLaje(corEsgotos, 0.95f), lisoEsg, null);
        Material matRejunteEsgotos = EnsureMaterial(madeira, "RejunteEsgotos", TomDoRejunte(corEsgotos), lisoEsg, null);
        ReflexoComoOPiso(matLajeEsgotos, esgotosMat);
        ReflexoComoOPiso(matLajeEsgotosB, esgotosMat);
        ReflexoComoOPiso(matRejunteEsgotos, esgotosMat);

        // TijoloEsgotos: ~12% mais escuro que DungeonEsgotos
        Color corTijoloEsg = new Color(corEsgotos.r * 0.88f, corEsgotos.g * 0.88f, corEsgotos.b * 0.88f, 1f);
        Material matTijoloEsgotos = EnsureMaterial(madeira, "TijoloEsgotos", corTijoloEsg, 0.25f, null);

        float lisoProf = profMat != null ? profMat.GetFloat("_Smoothness") : 0.55f;
        Material matLajeProfundezas = EnsureMaterial(madeira, "LajeProfundezas", TomDaLaje(corProf, 1.00f), lisoProf, null);
        Material matLajeProfundezasB = EnsureMaterial(madeira, "LajeProfundezasB", TomDaLaje(corProf, 0.95f), lisoProf, null);
        Material matRejunteProfundezas = EnsureMaterial(madeira, "RejunteProfundezas", TomDoRejunte(corProf), lisoProf, null);
        ReflexoComoOPiso(matLajeProfundezas, profMat);
        ReflexoComoOPiso(matLajeProfundezasB, profMat);
        ReflexoComoOPiso(matRejunteProfundezas, profMat);

        // TijoloProfundezas: ~12% mais escuro que DungeonProfundezas
        Color corTijoloProf = new Color(corProf.r * 0.88f, corProf.g * 0.88f, corProf.b * 0.88f, 1f);
        Material matTijoloProfundezas = EnsureMaterial(madeira, "TijoloProfundezas", corTijoloProf, 0.35f, null);

        // 2. Malhas procedurais facetadas em Blockout/Props/Malhas/
        Mesh meshRocha = SaveOrUpdateMesh("Rocha", BuildNoisyIcosphere(1, 101, 0.18f, new Vector3(1f, 0.92f, 1f)));
        Mesh meshCranio = SaveOrUpdateMesh("Cranio", BuildSkullMesh(202));
        Mesh meshSaco = SaveOrUpdateMesh("Saco", BuildSackMesh(303));
        Mesh meshChapeuCog = SaveOrUpdateMesh("ChapeuCogumelo", BuildConeMesh(7, 404));
        Mesh meshCristal = SaveOrUpdateMesh("Cristal", BuildStretchedOctahedron(505));
        // Rodada 2: musgo com 8 a 10 lados e ruido radial de +-35%, pra nenhuma mancha parecer elipse.
        Mesh meshMusgo1 = SaveOrUpdateMesh("Musgo1", BuildIrregularPolygonSlab(9, 701, 0.35f));
        Mesh meshMusgo2 = SaveOrUpdateMesh("Musgo2", BuildIrregularPolygonSlab(10, 702, 0.35f));
        Mesh meshMusgo3 = SaveOrUpdateMesh("Musgo3", BuildIrregularPolygonSlab(8, 703, 0.35f));
        Mesh meshSimbolo = SaveOrUpdateMesh("Simbolo", BuildCultSymbolMesh(801));
        Mesh meshTeia = SaveOrUpdateMesh("TeiaTriangulo", BuildWebTriangleMesh());
        Mesh meshVela = SaveOrUpdateMesh("Vela", BuildPrismMesh(6));
        Mesh meshViga = SaveOrUpdateMesh("VigaDaPorta", BuildLintelMesh());
        Mesh meshSuporte = SaveOrUpdateMesh("SuporteDeFerro", BuildTorchBracketMesh());

        // 3. Prefabs dos props
        BuildPilastra(matPedraDungeon);
        BuildTijolosSoltos("TijolosSoltosEsgotos", matTijoloEsgotos, 11);
        BuildTijolosSoltos("TijolosSoltosProfundezas", matTijoloProfundezas, 22);
        BuildLajes("LajesEsgotos", matLajeEsgotos, matLajeEsgotosB, matRejunteEsgotos, 91);
        BuildLajes("LajesProfundezas", matLajeProfundezas, matLajeProfundezasB, matRejunteProfundezas, 80);
        BuildMusgo(matMusgo, meshMusgo1, meshMusgo2, meshMusgo3);
        BuildRochedo(matPedraDungeon, meshRocha);
        BuildOssada(matOsso, meshCranio);
        BuildTeia(matTecido, meshTeia);
        BuildCantoDeLixo(madeira, matTecido, matOsso, meshSaco, meshCranio);
        BuildSacos(matTecido, meshSaco);
        BuildCorrentes(matFerro);
        BuildGradeDeDreno(matFerro);
        BuildBocaDeEsgoto(matPedraDungeon, matFerro, pocaVerde != null ? pocaVerde : matMusgo, meshMusgo1);
        BuildTabuas(madeira);
        BuildAltar(matPedraDungeon, matOsso, meshVela, meshCranio);
        BuildSimboloDoCulto(matSimboloCulto, meshSimbolo);
        BuildCristais(matCristalRoxo, meshCristal);
        BuildCogumelos(matOsso, matSimboloCulto, meshChapeuCog);
        BuildEstandarteCulto(madeira, matFerro, matSimboloCulto);
        BuildCranios(matOsso, meshCranio);

        // Rodada 2: pilhas montadas de parede, no lugar dos props soltos um a um.
        BuildPilhaDeCarga(madeira, matTecido, meshSaco);
        BuildPilhaDeOssos(matPedraDungeon, matOsso, meshRocha, meshCranio);
        BuildPilhaEsgoto(madeira, matTecido, pocaVerde != null ? pocaVerde : matMusgo, meshSaco, meshMusgo2);
        BuildCantoDoCulto(matPedraDungeon, matOsso, matCristalRoxo, meshVela, meshCranio, meshCristal);

        // Parte 2.1: VigaDaPorta.prefab (malha no proprio objeto raiz, sem filho)
        BuildVigaDaPorta(madeira, meshViga);
        AtualizarTochaGuia(matFerro, meshSuporte);
        LigarVigaNoGerador();

        // Parte 1b: Atualizar PropsEsgotos.asset e PropsProfundezas.asset
        AtualizarPropSets();

        // Parte 3: Variantes de salas normais e registro no DungeonGenerator.prefab
        GerarVariantesDeSala(madeira, pocaVerde != null ? pocaVerde : matMusgo);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CharmePropBuilder] Materiais, malhas, prefabs e PropSets gerados com sucesso.");
    }

    // =====================================================================
    // PASTAS E MATERIAIS
    // =====================================================================

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(PropsDir))
            AssetDatabase.CreateFolder(BlockoutRoot, "Props");
        if (!AssetDatabase.IsValidFolder(MeshDir))
            AssetDatabase.CreateFolder(PropsDir, "Malhas");
    }

    private static Material EnsureMaterial(Material templateMat, string name, Color baseColor, float smoothness, Color? emission)
    {
        string path = $"{MatDir}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            if (templateMat != null && AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(templateMat), path))
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            else
            {
                Shader sh = Shader.Find("Vermins/DungeonLit");
                mat = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
        }

        if (templateMat != null && mat.shader != templateMat.shader)
            mat.shader = templateMat.shader;

        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", smoothness);

        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission.Value);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// O piso do tema tem o reflexo do ambiente desligado, e o que sai do Madeira.mat vem com
    /// ele ligado. Com o smoothness 0,55 das Profundezas, a laje perto da tocha ficava ate 1,4x
    /// mais clara que o piso em volta; com o reflexo igual ao do piso, 1,0x.
    /// </summary>
    private static void ReflexoComoOPiso(Material mat, Material piso)
    {
        if (piso == null)
            return;

        float valor = piso.GetFloat("_EnvironmentReflections");
        mat.SetFloat("_EnvironmentReflections", valor);
        if (valor < 0.5f)
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        else
            mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(mat);
    }

    private static float LuminanciaLinear(Color lin)
    {
        return 0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b;
    }

    /// <summary>
    /// Cor da laje a partir do piso do tema: puxo 30% para um cinza azulado de mesma
    /// luminancia e acerto a luminancia linear para "valor" vezes a do piso. Faco a conta
    /// em linear porque e assim que a verificacao mede na foto.
    /// </summary>
    private static Color TomDaLaje(Color piso, float valor)
    {
        Color lin = piso.linear;
        float lum = LuminanciaLinear(lin);
        Color cinzaAzul = new Color(lum * 0.94f, lum, lum * 1.12f, 1f);
        Color mix = Color.Lerp(lin, cinzaAzul, 0.3f);
        float k = valor * lum / Mathf.Max(0.0001f, LuminanciaLinear(mix));
        return new Color(mix.r * k, mix.g * k, mix.b * k, 1f).gamma;
    }

    /// <summary>
    /// Rejunte: a cor do piso com o valor (HSV) 35% menor.
    /// </summary>
    private static Color TomDoRejunte(Color piso)
    {
        Color.RGBToHSV(piso, out float h, out float s, out float v);
        Color c = Color.HSVToRGB(h, s, v * 0.65f);
        c.a = 1f;
        return c;
    }

    private static Mesh SaveOrUpdateMesh(string name, Mesh newMesh)
    {
        newMesh.name = name;
        string path = $"{MeshDir}/{name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            // Rodada 2: copio pela API em vez de CopySerialized. Com CopySerialized o asset
            // mudava, mas o Play no mesmo editor ainda desenhava a malha velha da GPU.
            existing.Clear();
            existing.vertices = newMesh.vertices;
            existing.normals = newMesh.normals;
            existing.uv = newMesh.uv;
            existing.triangles = newMesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(newMesh);
            return existing;
        }

        AssetDatabase.CreateAsset(newMesh, path);
        return newMesh;
    }

    // =====================================================================
    // MALHAS PROCEDURAIS FACETADAS (LOW POLY)
    // =====================================================================

    private static Mesh FinalizeFacetedMesh(List<Vector3> rawVerts, List<int> rawTris, bool normalizeToUnitBox)
    {
        if (normalizeToUnitBox && rawVerts.Count > 0)
        {
            Vector3 min = rawVerts[0], max = rawVerts[0];
            for (int i = 1; i < rawVerts.Count; i++)
            {
                min = Vector3.Min(min, rawVerts[i]);
                max = Vector3.Max(max, rawVerts[i]);
            }
            Vector3 size = max - min;
            if (size.x < 0.0001f) size.x = 1f;
            if (size.y < 0.0001f) size.y = 1f;
            if (size.z < 0.0001f) size.z = 1f;

            for (int i = 0; i < rawVerts.Count; i++)
            {
                Vector3 v = rawVerts[i];
                float nx = (v.x - min.x) / size.x - 0.5f;
                float ny = (v.y - min.y) / size.y; // [0, 1] in Y so bottom sits at y=0
                float nz = (v.z - min.z) / size.z - 0.5f;
                rawVerts[i] = new Vector3(nx, ny, nz);
            }
        }

        int triCount = rawTris.Count;
        Vector3[] verts = new Vector3[triCount];
        Vector3[] norms = new Vector3[triCount];
        Vector2[] uvs = new Vector2[triCount];
        int[] tris = new int[triCount];

        for (int i = 0; i < triCount; i += 3)
        {
            Vector3 v0 = rawVerts[rawTris[i]];
            Vector3 v1 = rawVerts[rawTris[i + 1]];
            Vector3 v2 = rawVerts[rawTris[i + 2]];
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

            verts[i] = v0;
            verts[i + 1] = v1;
            verts[i + 2] = v2;
            norms[i] = n;
            norms[i + 1] = n;
            norms[i + 2] = n;
            uvs[i] = new Vector2(v0.x + 0.5f, v0.z + 0.5f);
            uvs[i + 1] = new Vector2(v1.x + 0.5f, v1.z + 0.5f);
            uvs[i + 2] = new Vector2(v2.x + 0.5f, v2.z + 0.5f);
            tris[i] = i;
            tris[i + 1] = i + 1;
            tris[i + 2] = i + 2;
        }

        Mesh m = new Mesh();
        m.vertices = verts;
        m.normals = norms;
        m.uv = uvs;
        m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }

    private static Mesh BuildNoisyIcosphere(int subdivisions, int seed, float noiseAmp, Vector3 aspect)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        List<Vector3> verts = new List<Vector3>
        {
            new Vector3(-1,  t,  0).normalized, new Vector3( 1,  t,  0).normalized,
            new Vector3(-1, -t,  0).normalized, new Vector3( 1, -t,  0).normalized,
            new Vector3( 0, -1,  t).normalized, new Vector3( 0,  1,  t).normalized,
            new Vector3( 0, -1, -t).normalized, new Vector3( 0,  1, -t).normalized,
            new Vector3( t,  0, -1).normalized, new Vector3( t,  0,  1).normalized,
            new Vector3(-t,  0, -1).normalized, new Vector3(-t,  0,  1).normalized
        };

        List<int> tris = new List<int>
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };

        for (int s = 0; s < subdivisions; s++)
        {
            Dictionary<long, int> midCache = new Dictionary<long, int>();
            List<int> nextTris = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = GetMidpoint(a, b, verts, midCache);
                int bc = GetMidpoint(b, c, verts, midCache);
                int ca = GetMidpoint(c, a, verts, midCache);
                nextTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris = nextTris;
        }

        Random.State old = Random.state;
        Random.InitState(seed);
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 v = verts[i];
            float r = 1f + Random.Range(-noiseAmp, noiseAmp);
            v = Vector3.Scale(v * r, aspect);
            if (v.y < -0.35f) v.y = -0.35f; // Flat base so rocks sit stably
            verts[i] = v;
        }
        Random.state = old;

        return FinalizeFacetedMesh(verts, tris, true);
    }

    private static int GetMidpoint(int i0, int i1, List<Vector3> verts, Dictionary<long, int> cache)
    {
        long min = Mathf.Min(i0, i1), max = Mathf.Max(i0, i1);
        long key = (min << 32) + max;
        if (cache.TryGetValue(key, out int idx)) return idx;
        Vector3 mid = ((verts[i0] + verts[i1]) * 0.5f).normalized;
        idx = verts.Count;
        verts.Add(mid);
        cache[key] = idx;
        return idx;
    }

    /// <summary>
    /// Icosfera de 40 triangulos com feicao de cranio (frente achatada com maxilar,
    /// topo arredondado), bem abaixo do limite mesmo em pilhas de 5-6 cranios.
    /// </summary>
    private static Mesh BuildSkullMesh(int seed)
    {
        // Subdivided octahedron (32 tris) + jaw protrusion -> 32 tris
        List<Vector3> verts = new List<Vector3>
        {
            new Vector3(0, 1, 0), new Vector3(0, -1, 0),
            new Vector3(-1, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 0, 1), new Vector3(0, 0, -1)
        };
        List<int> tris = new List<int>
        {
            0,4,3, 0,3,5, 0,5,2, 0,2,4,
            1,3,4, 1,5,3, 1,2,5, 1,4,2
        };

        Dictionary<long, int> midCache = new Dictionary<long, int>();
        List<int> nextTris = new List<int>();
        for (int i = 0; i < tris.Count; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            int ab = GetMidpoint(a, b, verts, midCache);
            int bc = GetMidpoint(b, c, verts, midCache);
            int ca = GetMidpoint(c, a, verts, midCache);
            nextTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        tris = nextTris;

        Random.State old = Random.state;
        Random.InitState(seed);
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 v = verts[i];
            float n = 1f + Random.Range(-0.08f, 0.08f);
            v *= n;
            // Shape like a skull: narrower lower half, slightly elongated front (+Z)
            if (v.y < 0f)
            {
                v.x *= 0.72f;
                if (v.z > 0.1f) v.z *= 1.18f;
                else v.z *= 0.78f;
            }
            verts[i] = v;
        }
        Random.state = old;

        return FinalizeFacetedMesh(verts, tris, true);
    }

    private static Mesh BuildSackMesh(int seed)
    {
        // Subdivided octahedron (32 tris) + tied top knot (12 tris) = 44 tris
        Mesh baseM = BuildNoisyIcosphere(0, seed, 0.12f, new Vector3(1.1f, 0.9f, 1.0f));
        Object.DestroyImmediate(baseM);

        List<Vector3> verts = new List<Vector3>
        {
            new Vector3(0, 1, 0), new Vector3(0, -1, 0),
            new Vector3(-1, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 0, 1), new Vector3(0, 0, -1)
        };
        List<int> tris = new List<int>
        {
            0,4,3, 0,3,5, 0,5,2, 0,2,4,
            1,3,4, 1,5,3, 1,2,5, 1,4,2
        };
        Dictionary<long, int> midCache = new Dictionary<long, int>();
        List<int> nextTris = new List<int>();
        for (int i = 0; i < tris.Count; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            int ab = GetMidpoint(a, b, verts, midCache);
            int bc = GetMidpoint(b, c, verts, midCache);
            int ca = GetMidpoint(c, a, verts, midCache);
            nextTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        tris = nextTris;

        Random.State old = Random.state;
        Random.InitState(seed);
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 v = verts[i];
            float bulge = v.y < 0.2f ? 1.18f : 0.85f;
            v.x *= bulge * (1f + Random.Range(-0.1f, 0.1f));
            v.z *= bulge * (1f + Random.Range(-0.1f, 0.1f));
            if (v.y < -0.55f) v.y = -0.55f;
            verts[i] = v;
        }
        Random.state = old;

        return FinalizeFacetedMesh(verts, tris, true);
    }

    private static Mesh BuildConeMesh(int sides, int seed)
    {
        Random.State old = Random.state;
        Random.InitState(seed);

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        verts.Add(new Vector3(0f, 1f, 0f)); // 0: apex
        for (int i = 0; i < sides; i++)
        {
            float ang = (i / (float)sides) * Mathf.PI * 2f;
            float r = 0.5f * (1f + Random.Range(-0.08f, 0.08f));
            verts.Add(new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
        }
        Random.state = old;

        // Side triangles
        for (int i = 0; i < sides; i++)
        {
            int curr = 1 + i;
            int next = 1 + ((i + 1) % sides);
            tris.Add(0);
            tris.Add(next);
            tris.Add(curr);
        }
        // Bottom cap fan
        for (int i = 1; i < sides - 1; i++)
        {
            tris.Add(1);
            tris.Add(1 + i);
            tris.Add(1 + i + 1);
        }

        return FinalizeFacetedMesh(verts, tris, true);
    }

    private static Mesh BuildStretchedOctahedron(int seed)
    {
        Random.State old = Random.state;
        Random.InitState(seed);

        // 6-sided bipyramid crystal (24 tris) for nicer crystal facets, or 4-sided octahedron (8 tris)
        List<Vector3> verts = new List<Vector3>
        {
            new Vector3(0f, 1f, 0f),     // 0: top tip
            new Vector3(0f, 0f, 0f),     // 1: bottom base center
            new Vector3(0.5f, 0.62f, 0f),
            new Vector3(0f, 0.65f, 0.5f),
            new Vector3(-0.5f, 0.60f, 0f),
            new Vector3(0f, 0.63f, -0.5f),
            new Vector3(0.35f, 0.02f, 0f),
            new Vector3(0f, 0.02f, 0.35f),
            new Vector3(-0.35f, 0.02f, 0f),
            new Vector3(0f, 0.02f, -0.35f),
        };
        Random.state = old;

        List<int> tris = new List<int>
        {
            // Upper pyramid
            0,3,2, 0,4,3, 0,5,4, 0,2,5,
            // Shaft
            2,3,7, 2,7,6,
            3,4,8, 3,8,7,
            4,5,9, 4,9,8,
            5,2,6, 5,6,9,
            // Bottom cap
            1,6,7, 1,7,8, 1,8,9, 1,9,6
        };

        return FinalizeFacetedMesh(verts, tris, true);
    }

    private static Mesh BuildIrregularPolygonSlab(int sides, int seed, float irregularity)
    {
        Random.State old = Random.state;
        Random.InitState(seed);

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int i = 0; i < sides; i++)
        {
            float ang = (i / (float)sides) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
            float r = 0.5f * (1f + Random.Range(-irregularity, irregularity));
            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;
            verts.Add(new Vector3(x, 0f, z)); // bottom ring: 0..sides-1
        }
        for (int i = 0; i < sides; i++)
        {
            Vector3 b = verts[i];
            verts.Add(new Vector3(b.x * 0.96f, 1f, b.z * 0.96f)); // top ring: sides..2*sides-1
        }
        Random.state = old;

        // Top face
        for (int i = 1; i < sides - 1; i++)
        {
            tris.Add(sides);
            tris.Add(sides + i + 1);
            tris.Add(sides + i);
        }
        // Bottom face
        for (int i = 1; i < sides - 1; i++)
        {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i + 1);
        }
        // Side bevels
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int b0 = i, b1 = next;
            int t0 = sides + i, t1 = sides + next;
            tris.Add(b0); tris.Add(t0); tris.Add(t1);
            tris.Add(b0); tris.Add(t1); tris.Add(b1);
        }

        return FinalizeFacetedMesh(verts, tris, true);
    }

    private static Mesh BuildCultSymbolMesh(int seed)
    {
        Random.State old = Random.state;
        Random.InitState(seed);
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        // Outer 8-sided ring + inner triangle bars
        AddFlatRing(verts, tris, 8, 0.44f, 0.50f, 0.03f);
        AddFlatRing(verts, tris, 8, 0.34f, 0.38f, 0.02f);

        // Two overlapping equilateral triangles (hexagram)
        for (int t = 0; t < 2; t++)
        {
            float baseAng = t * Mathf.PI / 3f;
            for (int e = 0; e < 3; e++)
            {
                float a0 = baseAng + e * (Mathf.PI * 2f / 3f);
                float a1 = baseAng + ((e + 1) % 3) * (Mathf.PI * 2f / 3f);
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.42f, 0f, Mathf.Sin(a0) * 0.42f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.42f, 0f, Mathf.Sin(a1) * 0.42f);
                AddFlatBar(verts, tris, p0, p1, 0.035f);
            }
        }

        Random.state = old;
        return FinalizeFacetedMesh(verts, tris, true);
    }

    private static void AddFlatRing(List<Vector3> verts, List<int> tris, int segments, float rIn, float rOut, float jitter)
    {
        int baseIdx = verts.Count;
        for (int i = 0; i < segments; i++)
        {
            float ang = (i / (float)segments) * Mathf.PI * 2f;
            float j = Random.Range(-jitter, jitter);
            float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
            verts.Add(new Vector3(c * (rIn + j), 0f, s * (rIn + j)));
            verts.Add(new Vector3(c * (rOut + j), 0f, s * (rOut + j)));
            verts.Add(new Vector3(c * (rIn + j), 1f, s * (rIn + j)));
            verts.Add(new Vector3(c * (rOut + j), 1f, s * (rOut + j)));
        }

        for (int i = 0; i < segments; i++)
        {
            int c0 = baseIdx + i * 4;
            int c1 = baseIdx + ((i + 1) % segments) * 4;
            // Top
            tris.AddRange(new[] { c0 + 2, c1 + 3, c0 + 3, c0 + 2, c1 + 2, c1 + 3 });
            // Outer side
            tris.AddRange(new[] { c0 + 1, c0 + 3, c1 + 3, c0 + 1, c1 + 3, c1 + 1 });
            // Inner side
            tris.AddRange(new[] { c0, c1 + 2, c0 + 2, c0, c1, c1 + 2 });
        }
    }

    private static void AddFlatBar(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, float width)
    {
        Vector3 dir = (b - a).normalized;
        Vector3 perp = new Vector3(-dir.z, 0f, dir.x) * (width * 0.5f);
        int idx = verts.Count;
        verts.Add(a - perp);
        verts.Add(a + perp);
        verts.Add(b + perp);
        verts.Add(b - perp);
        verts.Add(a - perp + Vector3.up);
        verts.Add(a + perp + Vector3.up);
        verts.Add(b + perp + Vector3.up);
        verts.Add(b - perp + Vector3.up);

        // Top + 2 long sides (6 tris)
        tris.AddRange(new[] {
            idx + 4, idx + 6, idx + 5, idx + 4, idx + 7, idx + 6,
            idx + 0, idx + 4, idx + 7, idx + 0, idx + 7, idx + 3,
            idx + 1, idx + 2, idx + 6, idx + 1, idx + 6, idx + 5
        });
    }

    private static Mesh BuildWebTriangleMesh()
    {
        List<Vector3> verts = new List<Vector3>
        {
            new Vector3(0f, 0f, -0.5f),
            new Vector3(-0.5f, 0.9f, 0.3f),
            new Vector3(0.5f, 1.0f, 0.3f),
            new Vector3(0f, 0.03f, -0.5f),
            new Vector3(-0.5f, 0.93f, 0.3f),
            new Vector3(0.5f, 1.03f, 0.3f),
        };
        List<int> tris = new List<int>
        {
            0, 1, 2,
            3, 5, 4,
            0, 2, 5, 0, 5, 3,
            1, 4, 5, 1, 5, 2,
            0, 3, 4, 0, 4, 1
        };
        return FinalizeFacetedMesh(verts, tris, false);
    }

    private static Mesh BuildPrismMesh(int sides)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float ang = (i / (float)sides) * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(ang) * 0.5f, 0f, Mathf.Sin(ang) * 0.5f));
        }
        for (int i = 0; i < sides; i++)
        {
            Vector3 b = verts[i];
            verts.Add(new Vector3(b.x, 1f, b.z));
        }
        for (int i = 1; i < sides - 1; i++)
        {
            tris.Add(sides); tris.Add(sides + i + 1); tris.Add(sides + i);
            tris.Add(0); tris.Add(i); tris.Add(i + 1);
        }
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            tris.Add(i); tris.Add(sides + i); tris.Add(sides + next);
            tris.Add(i); tris.Add(sides + next); tris.Add(next);
        }
        return FinalizeFacetedMesh(verts, tris, false);
    }

    /// <summary>
    /// Parte 2.1: Viga de 3,9 x 0,35 x 0,5 m com centro a 2,9 m de altura,
    /// mais 2 maos-francesas finas nas pontas, numa unica malha na raiz.
    /// </summary>
    private static Mesh BuildLintelMesh()
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        // Main horizontal beam: 3.9 x 0.35 x 0.5 m centered at (0, 2.9, 0)
        AddBoxToMesh(verts, tris, new Vector3(0f, 2.90f, 0f), new Vector3(3.90f, 0.35f, 0.50f), Quaternion.identity);

        // 2 angled braces (maos-francesas) at the ends against the pillars (pillars inner face is at x = +-1.60m)
        AddBoxToMesh(verts, tris, new Vector3(-1.38f, 2.52f, 0f), new Vector3(0.65f, 0.14f, 0.22f), Quaternion.Euler(0f, 0f, 42f));
        AddBoxToMesh(verts, tris, new Vector3(1.38f, 2.52f, 0f), new Vector3(0.65f, 0.14f, 0.22f), Quaternion.Euler(0f, 0f, -42f));

        return FinalizeFacetedMesh(verts, tris, false);
    }

    private static Mesh BuildTorchBracketMesh()
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        // Placa na parede (0.15 x 0.30 x 0.04 m) encostando em z = -0.06 (wallGap do DungeonGuide)
        AddBoxToMesh(verts, tris, new Vector3(0f, 1.42f, -0.04f), new Vector3(0.15f, 0.30f, 0.04f), Quaternion.identity);
        // Braco horizontal ate o cabo da tocha (em z = 0)
        AddBoxToMesh(verts, tris, new Vector3(0f, 1.48f, -0.005f), new Vector3(0.05f, 0.045f, 0.07f), Quaternion.identity);
        // Mao-francesa diagonal de apoio
        AddBoxToMesh(verts, tris, new Vector3(0f, 1.38f, -0.018f), new Vector3(0.04f, 0.14f, 0.035f), Quaternion.Euler(26f, 0f, 0f));
        // Anel/abracadeira em volta da Haste (raio 0.04)
        AddBoxToMesh(verts, tris, new Vector3(0f, 1.48f, 0.00f), new Vector3(0.105f, 0.05f, 0.105f), Quaternion.identity);

        return FinalizeFacetedMesh(verts, tris, false);
    }

    private static void AddBoxToMesh(List<Vector3> verts, List<int> tris, Vector3 center, Vector3 size, Quaternion rot)
    {
        Vector3 h = size * 0.5f;
        Vector3[] local =
        {
            new Vector3(-h.x, -h.y, -h.z), new Vector3( h.x, -h.y, -h.z),
            new Vector3( h.x,  h.y, -h.z), new Vector3(-h.x,  h.y, -h.z),
            new Vector3(-h.x, -h.y,  h.z), new Vector3( h.x, -h.y,  h.z),
            new Vector3( h.x,  h.y,  h.z), new Vector3(-h.x,  h.y,  h.z)
        };
        int b = verts.Count;
        for (int i = 0; i < 8; i++)
            verts.Add(center + rot * local[i]);

        int[] faces =
        {
            0,2,1, 0,3,2, // back (-Z)
            4,5,6, 4,6,7, // front (+Z)
            0,1,5, 0,5,4, // bottom (-Y)
            3,7,6, 3,6,2, // top (+Y)
            0,4,7, 0,7,3, // left (-X)
            1,2,6, 1,6,5  // right (+X)
        };
        for (int i = 0; i < faces.Length; i++)
            tris.Add(b + faces[i]);
    }

    // =====================================================================
    // HELPERS DE CONSTRUCAO DE PREFABS (SEM COLLIDER, SEM LIGHT)
    // =====================================================================

    private static GameObject AddPrimitive(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static GameObject AddCustomMesh(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    private static void SavePropPrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    // =====================================================================
    // CONSTRUCAO DOS 19 PROPS (21 PREFABS) + VIGADAPORTA
    // =====================================================================

    private static void BuildPilastra(Material matPedra)
    {
        // Wall, size 0.6 x 0.35 -> wall at z = -0.205
        GameObject root = new GameObject("Pilastra");
        AddPrimitive(root.transform, "Coluna", PrimitiveType.Cube, new Vector3(0f, 1.55f, -0.04f), new Vector3(0.46f, 3.10f, 0.26f), Quaternion.identity, matPedra);
        AddPrimitive(root.transform, "Base", PrimitiveType.Cube, new Vector3(0f, 0.14f, -0.03f), new Vector3(0.54f, 0.28f, 0.30f), Quaternion.identity, matPedra);
        AddPrimitive(root.transform, "Capitel", PrimitiveType.Cube, new Vector3(0f, 2.98f, -0.03f), new Vector3(0.54f, 0.24f, 0.30f), Quaternion.identity, matPedra);
        SavePropPrefab(root, $"{PropsDir}/Pilastra.prefab");
    }

    private static void BuildTijolosSoltos(string name, Material mat, int seed)
    {
        // Wall, size 1.2 x 0.08 -> wall at z = -0.07
        // 5 a 8 blocos de 0,4 x 0,15 x 0,05 m na face da parede, de 0,3 a 2,6 m de altura, em fiadas tortas
        GameObject root = new GameObject(name);
        Vector3[] positions =
        {
            new Vector3(-0.28f, 0.42f, -0.045f),
            new Vector3( 0.14f, 0.44f, -0.045f),
            new Vector3(-0.08f, 0.85f, -0.045f),
            new Vector3( 0.28f, 1.25f, -0.045f),
            new Vector3(-0.22f, 1.48f, -0.045f),
            new Vector3( 0.10f, 1.98f, -0.045f),
            new Vector3(-0.18f, 2.42f, -0.045f)
        };
        float[] tilts = { -3.5f, 2.8f, -2.0f, 4.0f, -3.0f, 2.5f, -4.2f };

        for (int i = 0; i < positions.Length; i++)
        {
            float flip = (seed % 2 == 0) ? 1f : -1f;
            Vector3 p = positions[i];
            p.x *= flip;
            Quaternion r = Quaternion.Euler(0f, 0f, tilts[i] * flip);
            AddPrimitive(root.transform, $"Tijolo_{i + 1}", PrimitiveType.Cube, p, new Vector3(0.40f, 0.15f, 0.05f), r, mat);
        }
        SavePropPrefab(root, $"{PropsDir}/{name}.prefab");
    }

    /// <summary>
    /// Rodada 2. Floor 3 x 3 com snapRotation: lajes retangulares em grade sobre um rejunte
    /// escuro. Monto em fileiras de fundo sorteado, cada uma com lajes de largura sorteada e a
    /// junta desencontrada, como piso de pedra assentado, num quadrado de 3 m. Quadrado e nao
    /// circulo porque o giro e em multiplo de 90 e as paredes das salas tambem: o FloorSpot ja
    /// garante a distancia ate a parede de cada lado. No circulo de 1,45 m so cabiam 5 lajes.
    ///
    /// O rejunte e a uniao das lajes alargadas 3 cm de cada lado, com 0,01 m: uma placa
    /// embaixo de cada laje. Como a folga entre lajes e de 4 a 6 cm, ele aparece so nas juntas
    /// e num fio em volta, e a laje que falta na borda some junto, deixando o contorno
    /// irregular. Comecei com uma malha so, mas regravada por cima com CopySerialized ela
    /// apareceu no Play com placas de uma versao velha; com cubo isso nao acontece.
    /// </summary>
    private static void BuildLajes(string name, Material matA, Material matB, Material matRejunte, int seed)
    {
        const float meio = 1.5f;
        const float altRejunte = 0.01f;
        const float espessura = 0.03f;

        Random.State old = Random.state;
        Random.InitState(seed);

        // Encho o quadrado: fileira enquanto couber, e na fileira laje enquanto couber. Cada
        // fileira comeca num ponto sorteado, pra junta nao alinhar com a de cima.
        List<Vector4> lajes = new List<Vector4>(); // x, z, largura, fundo
        float z = -meio + Random.Range(0f, 0.1f);
        while (true)
        {
            float fundo = Random.Range(0.5f, 0.8f);
            if (z + fundo > meio)
                break;

            float x = -meio + Random.Range(0f, 0.2f);
            while (true)
            {
                float largura = Random.Range(0.6f, 0.9f);
                if (x + largura > meio)
                    break;
                lajes.Add(new Vector4(x + largura * 0.5f, z + fundo * 0.5f, largura, fundo));
                x += largura + Random.Range(0.04f, 0.06f);
            }
            z += fundo + Random.Range(0.04f, 0.06f);
        }

        // O DungeonDressing separa os props do chao por circulo (raio da metade do size, mais
        // 0,25 m de folga), e a quina do quadrado passa disso: na auditoria uma poca ficou
        // embaixo da laje. Tiro a laje que tem quina a mais de 1,6 m do centro (as 4 dos cantos).
        lajes.RemoveAll(l => new Vector2(Mathf.Abs(l.x) + l.z * 0.5f + 0.03f, Mathf.Abs(l.y) + l.w * 0.5f + 0.03f).magnitude > 1.6f);

        // Alem das quinas, tiro 1 a 2 lajes da metade de fora (sorteadas, nao as mais longe), pro
        // contorno nao ficar simetrico, e no maximo 12 ficam.
        lajes.Sort((a, b) => (a.x * a.x + a.y * a.y).CompareTo(b.x * b.x + b.y * b.y));
        int tirar = Random.Range(1, 3);
        for (int i = 0; i < tirar && lajes.Count > 7; i++)
            lajes.RemoveAt(Random.Range(lajes.Count / 2, lajes.Count));
        while (lajes.Count > 12)
            lajes.RemoveAt(lajes.Count - 1);

        GameObject root = new GameObject(name);
        Transform rejunte = new GameObject("Rejunte").transform;
        rejunte.SetParent(root.transform, false);

        for (int i = 0; i < lajes.Count; i++)
        {
            Vector4 l = lajes[i];
            float yaw = Random.Range(-4f, 4f);
            Quaternion giro = Quaternion.Euler(Random.Range(-1f, 1f), yaw, Random.Range(-1f, 1f));
            Material mat = Random.value < 0.5f ? matA : matB;

            AddPrimitive(rejunte, $"Rejunte_{i + 1}", PrimitiveType.Cube, new Vector3(l.x, altRejunte * 0.5f, l.y), new Vector3(l.z + 0.06f, altRejunte, l.w + 0.06f), Quaternion.Euler(0f, yaw, 0f), matRejunte);
            AddPrimitive(root.transform, $"Laje_{i + 1}", PrimitiveType.Cube, new Vector3(l.x, altRejunte + espessura * 0.5f, l.y), new Vector3(l.z, espessura, l.w), giro, mat);
        }
        Random.state = old;
        SavePropPrefab(root, $"{PropsDir}/{name}.prefab");
    }

    /// <summary>
    /// Rodada 2. Floor 1,4 x 1,4: 4 manchas pequenas (0,3 a 0,6 m) agrupadas, cada uma com 8 a
    /// 10 lados e ruido radial de +-35%, a ~0,015 m. A elipse lisa de 1,4 m parecia tapete.
    /// Cada mancha tem uma altura um pouco diferente (13 a 17 mm), pra onde elas se cruzam
    /// nao dar z-fighting.
    /// </summary>
    private static void BuildMusgo(Material mat, Mesh m1, Mesh m2, Mesh m3)
    {
        GameObject root = new GameObject("Musgo");
        AddCustomMesh(root.transform, "Mancha_1", m1, new Vector3(-0.22f, 0f, -0.10f), new Vector3(0.60f, 0.015f, 0.50f), Quaternion.Euler(0f, 15f, 0f), mat);
        AddCustomMesh(root.transform, "Mancha_2", m2, new Vector3( 0.28f, 0f,  0.14f), new Vector3(0.46f, 0.013f, 0.40f), Quaternion.Euler(0f, 110f, 0f), mat);
        AddCustomMesh(root.transform, "Mancha_3", m3, new Vector3(-0.02f, 0f,  0.40f), new Vector3(0.34f, 0.017f, 0.30f), Quaternion.Euler(0f, 230f, 0f), mat);
        AddCustomMesh(root.transform, "Mancha_4", m3, new Vector3( 0.18f, 0f, -0.40f), new Vector3(0.30f, 0.014f, 0.28f), Quaternion.Euler(0f, 300f, 0f), mat);
        SavePropPrefab(root, $"{PropsDir}/Musgo.prefab");
    }

    private static void BuildRochedo(Material mat, Mesh meshRocha)
    {
        // Corner, 0.9 x 0.9 (rodada 2; era 1,0 x 1,0): 2 a 4 icosferas de PedraDungeon empilhadas,
        // de 1,2 a 2,2 m de altura. Com 1 m de base o dressing so achava canto pra ele em ~0,1
        // sala por sala; encolhi a planta em 10% e mantive a altura.
        GameObject root = new GameObject("Rochedo");
        AddCustomMesh(root.transform, "RochaBase_1", meshRocha, new Vector3(-0.07f, 0f, -0.07f), new Vector3(0.61f, 0.85f, 0.59f), Quaternion.Euler(0f, 20f, 0f), mat);
        AddCustomMesh(root.transform, "RochaBase_2", meshRocha, new Vector3( 0.14f, 0f,  0.07f), new Vector3(0.43f, 0.56f, 0.41f), Quaternion.Euler(0f, 135f, 0f), mat);
        AddCustomMesh(root.transform, "RochaTopo",   meshRocha, new Vector3(-0.05f, 0.68f, -0.05f), new Vector3(0.47f, 0.82f, 0.45f), Quaternion.Euler(0f, 250f, 0f), mat);
        SavePropPrefab(root, $"{PropsDir}/Rochedo.prefab");
    }

    private static void BuildOssada(Material matOsso, Mesh meshCranio)
    {
        // Floor, 0.8 x 0.6: cranio e 3 a 5 ossos (cilindros finos), ate 0,15 m
        GameObject root = new GameObject("Ossada");
        AddCustomMesh(root.transform, "Cranio", meshCranio, new Vector3(-0.10f, 0f, 0.02f), new Vector3(0.14f, 0.14f, 0.16f), Quaternion.Euler(0f, 32f, 0f), matOsso);
        AddPrimitive(root.transform, "Osso_1", PrimitiveType.Cylinder, new Vector3( 0.12f, 0.02f, -0.06f), new Vector3(0.036f, 0.18f, 0.036f), Quaternion.Euler(90f, 25f, 0f), matOsso);
        AddPrimitive(root.transform, "Osso_2", PrimitiveType.Cylinder, new Vector3( 0.04f, 0.02f,  0.12f), new Vector3(0.034f, 0.16f, 0.034f), Quaternion.Euler(90f, -50f, 0f), matOsso);
        AddPrimitive(root.transform, "Osso_3", PrimitiveType.Cylinder, new Vector3(-0.14f, 0.02f, -0.12f), new Vector3(0.032f, 0.14f, 0.032f), Quaternion.Euler(90f, 80f, 0f), matOsso);
        SavePropPrefab(root, $"{PropsDir}/Ossada.prefab");
    }

    private static void BuildTeia(Material matTecido, Mesh meshTeia)
    {
        // Corner, 0.6 x 0.6: 2 a 3 triangulos finos no alto do canto, de 2,0 a 2,9 m
        // Touches back wall (z = -0.33) and spans x in [-0.32, +0.32]
        GameObject root = new GameObject("Teia");
        AddCustomMesh(root.transform, "Fio_1", meshTeia, new Vector3(0f, 2.05f, -0.08f), new Vector3(0.62f, 0.78f, 0.52f), Quaternion.identity, matTecido);
        AddCustomMesh(root.transform, "Fio_2", meshTeia, new Vector3(-0.06f, 2.25f, -0.10f), new Vector3(0.54f, 0.56f, 0.46f), Quaternion.Euler(0f, 18f, 0f), matTecido);
        AddCustomMesh(root.transform, "Fio_3", meshTeia, new Vector3(0.06f, 2.15f, -0.10f), new Vector3(0.52f, 0.62f, 0.46f), Quaternion.Euler(0f, -18f, 0f), matTecido);
        SavePropPrefab(root, $"{PropsDir}/Teia.prefab");
    }

    private static void BuildCantoDeLixo(Material matMadeira, Material matTecido, Material matOsso, Mesh meshSaco, Mesh meshCranio)
    {
        // Corner, 1.0 x 1.0: caixa, saco e ossos juntos
        GameObject root = new GameObject("CantoDeLixo");
        AddPrimitive(root.transform, "Caixa", PrimitiveType.Cube, new Vector3(-0.14f, 0.23f, -0.12f), new Vector3(0.46f, 0.46f, 0.46f), Quaternion.Euler(0f, 8f, 0f), matMadeira);
        AddCustomMesh(root.transform, "Saco_1", meshSaco, new Vector3(0.18f, 0f, -0.10f), new Vector3(0.36f, 0.42f, 0.34f), Quaternion.Euler(0f, -15f, 0f), matTecido);
        AddCustomMesh(root.transform, "Saco_2", meshSaco, new Vector3(-0.02f, 0f, 0.14f), new Vector3(0.30f, 0.30f, 0.28f), Quaternion.Euler(0f, 40f, 0f), matTecido);
        AddCustomMesh(root.transform, "Cranio", meshCranio, new Vector3(0.16f, 0f, 0.18f), new Vector3(0.16f, 0.16f, 0.18f), Quaternion.Euler(0f, -30f, 0f), matOsso);
        AddPrimitive(root.transform, "Osso_1", PrimitiveType.Cylinder, new Vector3(-0.16f, 0.02f, 0.20f), new Vector3(0.036f, 0.16f, 0.036f), Quaternion.Euler(90f, 35f, 0f), matOsso);
        AddPrimitive(root.transform, "Osso_2", PrimitiveType.Cylinder, new Vector3(0.05f, 0.02f, 0.25f), new Vector3(0.034f, 0.14f, 0.034f), Quaternion.Euler(90f, -60f, 0f), matOsso);
        SavePropPrefab(root, $"{PropsDir}/CantoDeLixo.prefab");
    }

    private static void BuildSacos(Material matTecido, Mesh meshSaco)
    {
        // Wall, 0.9 x 0.5: 2 a 3 icosferas achatadas de Tecido (~0.5 m de altura)
        GameObject root = new GameObject("Sacos");
        AddCustomMesh(root.transform, "Saco_1", meshSaco, new Vector3(-0.18f, 0f, -0.02f), new Vector3(0.40f, 0.46f, 0.34f), Quaternion.Euler(0f, 12f, 0f), matTecido);
        AddCustomMesh(root.transform, "Saco_2", meshSaco, new Vector3( 0.18f, 0f, -0.01f), new Vector3(0.36f, 0.40f, 0.32f), Quaternion.Euler(0f, -18f, 0f), matTecido);
        AddCustomMesh(root.transform, "Saco_3", meshSaco, new Vector3( 0.01f, 0.22f, -0.03f), new Vector3(0.34f, 0.28f, 0.28f), Quaternion.Euler(0f, 45f, 0f), matTecido);
        SavePropPrefab(root, $"{PropsDir}/Sacos.prefab");
    }

    private static void BuildCorrentes(Material matFerro)
    {
        // Wall, 0.5 x 0.15: elos de toro achatado ou cilindros, pendurados de 1,2 a 2,6 m
        // Wall is at z = -0.105
        GameObject root = new GameObject("Correntes");
        AddPrimitive(root.transform, "SuporteEsq", PrimitiveType.Cube, new Vector3(-0.12f, 2.54f, -0.07f), new Vector3(0.08f, 0.10f, 0.08f), Quaternion.identity, matFerro);
        AddPrimitive(root.transform, "SuporteDir", PrimitiveType.Cube, new Vector3( 0.12f, 2.54f, -0.07f), new Vector3(0.08f, 0.10f, 0.08f), Quaternion.identity, matFerro);

        // Left chain: y from 1.25 to 2.52 (7 links)
        for (int i = 0; i < 7; i++)
        {
            float y = 2.45f - i * 0.19f;
            Quaternion r = (i % 2 == 0) ? Quaternion.Euler(0f, 15f, 0f) : Quaternion.Euler(0f, 85f, 0f);
            AddPrimitive(root.transform, $"EloEsq_{i + 1}", PrimitiveType.Cube, new Vector3(-0.12f, y, -0.04f), new Vector3(0.06f, 0.22f, 0.03f), r, matFerro);
        }
        // Right chain: y from 1.58 to 2.52 (5 links)
        for (int i = 0; i < 5; i++)
        {
            float y = 2.45f - i * 0.19f;
            Quaternion r = (i % 2 == 0) ? Quaternion.Euler(0f, -20f, 0f) : Quaternion.Euler(0f, 75f, 0f);
            AddPrimitive(root.transform, $"EloDir_{i + 1}", PrimitiveType.Cube, new Vector3(0.12f, y, -0.04f), new Vector3(0.06f, 0.22f, 0.03f), r, matFerro);
        }
        SavePropPrefab(root, $"{PropsDir}/Correntes.prefab");
    }

    private static void BuildGradeDeDreno(Material matFerro)
    {
        // Floor, 0.9 x 0.9: moldura e barras de Ferro, 0,05 m
        GameObject root = new GameObject("GradeDeDreno");
        float s = 0.80f;
        float h = 0.045f;
        float t = 0.06f;
        AddPrimitive(root.transform, "MolduraN", PrimitiveType.Cube, new Vector3(0f, h * 0.5f,  s * 0.5f), new Vector3(s + t, h, t), Quaternion.identity, matFerro);
        AddPrimitive(root.transform, "MolduraS", PrimitiveType.Cube, new Vector3(0f, h * 0.5f, -s * 0.5f), new Vector3(s + t, h, t), Quaternion.identity, matFerro);
        AddPrimitive(root.transform, "MolduraL", PrimitiveType.Cube, new Vector3( s * 0.5f, h * 0.5f, 0f), new Vector3(t, h, s - t), Quaternion.identity, matFerro);
        AddPrimitive(root.transform, "MolduraO", PrimitiveType.Cube, new Vector3(-s * 0.5f, h * 0.5f, 0f), new Vector3(t, h, s - t), Quaternion.identity, matFerro);

        for (int i = -2; i <= 2; i++)
        {
            float x = i * 0.13f;
            AddPrimitive(root.transform, $"Barra_{i + 3}", PrimitiveType.Cube, new Vector3(x, h * 0.45f, 0f), new Vector3(0.035f, h * 0.9f, s - t), Quaternion.identity, matFerro);
        }
        SavePropPrefab(root, $"{PropsDir}/GradeDeDreno.prefab");
    }

    private static void BuildBocaDeEsgoto(Material matPedra, Material matFerro, Material matPocaVerde, Mesh meshPoca)
    {
        // Wall, 1.0 x 0.6: cano de 0,6 m saindo da parede a 0,5 m de altura, com mancha verde plana embaixo
        // Wall is at z = -0.33
        GameObject root = new GameObject("BocaDeEsgoto");
        AddPrimitive(root.transform, "BordaPedra", PrimitiveType.Cube, new Vector3(0f, 0.55f, -0.25f), new Vector3(0.64f, 0.64f, 0.12f), Quaternion.identity, matPedra);
        AddPrimitive(root.transform, "Cano", PrimitiveType.Cylinder, new Vector3(0f, 0.55f, -0.05f), new Vector3(0.52f, 0.24f, 0.52f), Quaternion.Euler(90f, 0f, 0f), matFerro);
        AddPrimitive(root.transform, "JorroVerde", PrimitiveType.Cube, new Vector3(0f, 0.28f, 0.12f), new Vector3(0.28f, 0.52f, 0.08f), Quaternion.Euler(-12f, 0f, 0f), matPocaVerde);
        AddCustomMesh(root.transform, "ManchaVerde", meshPoca, new Vector3(0f, 0f, 0.02f), new Vector3(0.84f, 0.02f, 0.48f), Quaternion.identity, matPocaVerde);
        SavePropPrefab(root, $"{PropsDir}/BocaDeEsgoto.prefab");
    }

    private static void BuildTabuas(Material matMadeira)
    {
        // Floor, 1.2 x 0.8: 3 a 4 cubos finos tortos, ate 0,1 m
        GameObject root = new GameObject("Tabuas");
        AddPrimitive(root.transform, "Tabua_1", PrimitiveType.Cube, new Vector3(-0.08f, 0.018f, -0.16f), new Vector3(0.86f, 0.036f, 0.15f), Quaternion.Euler(0f, 8f, 0f), matMadeira);
        AddPrimitive(root.transform, "Tabua_2", PrimitiveType.Cube, new Vector3( 0.05f, 0.018f,  0.04f), new Vector3(0.92f, 0.036f, 0.16f), Quaternion.Euler(0f, -6f, 0f), matMadeira);
        AddPrimitive(root.transform, "Tabua_3", PrimitiveType.Cube, new Vector3(-0.04f, 0.018f,  0.22f), new Vector3(0.78f, 0.036f, 0.14f), Quaternion.Euler(0f, 14f, 0f), matMadeira);
        AddPrimitive(root.transform, "Tabua_4", PrimitiveType.Cube, new Vector3( 0.02f, 0.054f, -0.02f), new Vector3(0.74f, 0.036f, 0.14f), Quaternion.Euler(0f, -24f, 0f), matMadeira);
        SavePropPrefab(root, $"{PropsDir}/Tabuas.prefab");
    }

    private static void BuildAltar(Material matPedra, Material matOsso, Mesh meshVela, Mesh meshCranio)
    {
        // Wall, 1.6 x 0.7: bloco de PedraDungeon de 1,6 x 0,7 x 1,0 m, com 5 velas (sem luz) e 2 cranios em cima
        GameObject root = new GameObject("Altar");
        AddPrimitive(root.transform, "Base", PrimitiveType.Cube, new Vector3(0f, 0.07f, -0.02f), new Vector3(1.50f, 0.14f, 0.58f), Quaternion.identity, matPedra);
        AddPrimitive(root.transform, "Bloco", PrimitiveType.Cube, new Vector3(0f, 0.50f, -0.02f), new Vector3(1.42f, 0.84f, 0.52f), Quaternion.identity, matPedra);
        AddPrimitive(root.transform, "Topo", PrimitiveType.Cube, new Vector3(0f, 0.96f, -0.02f), new Vector3(1.52f, 0.08f, 0.60f), Quaternion.identity, matPedra);

        Vector3[] velaPos =
        {
            new Vector3(-0.56f, 1.00f, -0.12f),
            new Vector3(-0.46f, 1.00f,  0.04f),
            new Vector3( 0.00f, 1.00f, -0.14f),
            new Vector3( 0.48f, 1.00f, -0.10f),
            new Vector3( 0.58f, 1.00f,  0.05f)
        };
        float[] velaAlt = { 0.16f, 0.12f, 0.18f, 0.15f, 0.11f };
        for (int i = 0; i < 5; i++)
            AddCustomMesh(root.transform, $"Vela_{i + 1}", meshVela, velaPos[i], new Vector3(0.05f, velaAlt[i], 0.05f), Quaternion.identity, matOsso);

        AddCustomMesh(root.transform, "Cranio_1", meshCranio, new Vector3(-0.22f, 1.00f, 0.05f), new Vector3(0.18f, 0.18f, 0.20f), Quaternion.Euler(0f, 15f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_2", meshCranio, new Vector3( 0.24f, 1.00f, 0.04f), new Vector3(0.17f, 0.17f, 0.19f), Quaternion.Euler(0f, -20f, 0f), matOsso);
        SavePropPrefab(root, $"{PropsDir}/Altar.prefab");
    }

    private static void BuildSimboloDoCulto(Material matSimbolo, Mesh meshSimbolo)
    {
        // Floor, 2.4 x 2.4: anel e triangulo planos de SimboloCulto, 0,02 m, ~2,4 m
        GameObject root = new GameObject("SimboloDoCulto");
        AddCustomMesh(root.transform, "Simbolo", meshSimbolo, Vector3.zero, new Vector3(1.72f, 0.02f, 1.72f), Quaternion.identity, matSimbolo);
        SavePropPrefab(root, $"{PropsDir}/SimboloDoCulto.prefab");
    }

    private static void BuildCristais(Material matCristal, Mesh meshCristal)
    {
        // Corner, 0.8 x 0.8: 3 a 6 octaedros esticados e inclinados
        GameObject root = new GameObject("Cristais");
        AddCustomMesh(root.transform, "Cristal_1", meshCristal, new Vector3(-0.05f, 0.025f, -0.05f), new Vector3(0.20f, 0.80f, 0.20f), Quaternion.Euler(10f, 20f, -8f), matCristal);
        AddCustomMesh(root.transform, "Cristal_2", meshCristal, new Vector3( 0.10f, 0.025f, -0.04f), new Vector3(0.17f, 0.62f, 0.17f), Quaternion.Euler(14f, 80f, 12f), matCristal);
        AddCustomMesh(root.transform, "Cristal_3", meshCristal, new Vector3(-0.11f, 0.025f,  0.08f), new Vector3(0.15f, 0.50f, 0.15f), Quaternion.Euler(-12f, 150f, -14f), matCristal);
        AddCustomMesh(root.transform, "Cristal_4", meshCristal, new Vector3( 0.04f, 0.025f,  0.11f), new Vector3(0.13f, 0.38f, 0.13f), Quaternion.Euler(18f, -40f, 10f), matCristal);
        AddCustomMesh(root.transform, "Cristal_5", meshCristal, new Vector3(-0.02f, 0.025f,  0.02f), new Vector3(0.11f, 0.30f, 0.11f), Quaternion.Euler(-8f, 210f, 16f), matCristal);
        SavePropPrefab(root, $"{PropsDir}/Cristais.prefab");
    }

    private static void BuildCogumelos(Material matCaule, Material matChapeu, Mesh meshChapeu)
    {
        // Corner, 0.8 x 0.8: caule cilindro e chapeu cone, de 3 a 5 tamanhos (3 cogumelos = 276 tris <= 300)
        GameObject root = new GameObject("Cogumelos");
        Vector3[] pos = { new Vector3(-0.08f, 0f, -0.06f), new Vector3(0.12f, 0f, 0.04f), new Vector3(-0.04f, 0f, 0.14f) };
        float[] hCaule = { 0.34f, 0.24f, 0.16f };
        float[] rCaule = { 0.07f, 0.055f, 0.045f };
        float[] wChap = { 0.32f, 0.24f, 0.18f };
        float[] hChap = { 0.18f, 0.14f, 0.11f };

        for (int i = 0; i < 3; i++)
        {
            Vector3 p = pos[i];
            AddPrimitive(root.transform, $"Caule_{i + 1}", PrimitiveType.Cylinder, p + Vector3.up * (hCaule[i] * 0.5f), new Vector3(rCaule[i], hCaule[i] * 0.5f, rCaule[i]), Quaternion.identity, matCaule);
            AddCustomMesh(root.transform, $"Chapeu_{i + 1}", meshChapeu, p + Vector3.up * (hCaule[i] - 0.02f), new Vector3(wChap[i], hChap[i], wChap[i]), Quaternion.Euler(0f, i * 45f, 0f), matChapeu);
        }
        SavePropPrefab(root, $"{PropsDir}/Cogumelos.prefab");
    }

    private static void BuildEstandarteCulto(Material matMadeira, Material matFerro, Material matSimbolo)
    {
        // Wall, 0.8 x 0.15: haste e pano roxo (cubo fino) de 2,5 m
        // Wall is at z = -0.105
        GameObject root = new GameObject("EstandarteCulto");
        AddPrimitive(root.transform, "Haste", PrimitiveType.Cube, new Vector3(0f, 2.48f, -0.03f), new Vector3(0.68f, 0.05f, 0.05f), Quaternion.identity, matMadeira);
        AddPrimitive(root.transform, "SuporteEsq", PrimitiveType.Cube, new Vector3(-0.26f, 2.48f, -0.07f), new Vector3(0.05f, 0.06f, 0.08f), Quaternion.identity, matFerro);
        AddPrimitive(root.transform, "SuporteDir", PrimitiveType.Cube, new Vector3( 0.26f, 2.48f, -0.07f), new Vector3(0.05f, 0.06f, 0.08f), Quaternion.identity, matFerro);
        AddPrimitive(root.transform, "Pano", PrimitiveType.Cube, new Vector3(0f, 1.70f, -0.03f), new Vector3(0.54f, 1.52f, 0.02f), Quaternion.identity, matSimbolo);
        AddPrimitive(root.transform, "PontaEsq", PrimitiveType.Cube, new Vector3(-0.14f, 0.88f, -0.03f), new Vector3(0.24f, 0.24f, 0.02f), Quaternion.Euler(0f, 0f, 45f), matSimbolo);
        AddPrimitive(root.transform, "PontaDir", PrimitiveType.Cube, new Vector3( 0.14f, 0.88f, -0.03f), new Vector3(0.24f, 0.24f, 0.02f), Quaternion.Euler(0f, 0f, 45f), matSimbolo);
        SavePropPrefab(root, $"{PropsDir}/EstandarteCulto.prefab");
    }

    private static void BuildCranios(Material matOsso, Mesh meshCranio)
    {
        // Corner, 0.8 x 0.8: pilha de 4 a 6 cranios
        GameObject root = new GameObject("Cranios");
        AddCustomMesh(root.transform, "Cranio_1", meshCranio, new Vector3(-0.11f, 0f, -0.08f), new Vector3(0.20f, 0.20f, 0.22f), Quaternion.Euler(0f, 15f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_2", meshCranio, new Vector3( 0.11f, 0f, -0.07f), new Vector3(0.20f, 0.20f, 0.22f), Quaternion.Euler(0f, -20f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_3", meshCranio, new Vector3(-0.09f, 0f,  0.11f), new Vector3(0.19f, 0.19f, 0.21f), Quaternion.Euler(0f, 40f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_4", meshCranio, new Vector3( 0.10f, 0f,  0.10f), new Vector3(0.19f, 0.19f, 0.21f), Quaternion.Euler(0f, -35f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_Topo", meshCranio, new Vector3(0.00f, 0.15f, 0.01f), new Vector3(0.21f, 0.21f, 0.23f), Quaternion.Euler(-8f, 5f, 0f), matOsso);
        SavePropPrefab(root, $"{PropsDir}/Cranios.prefab");
    }

    // =====================================================================
    // RODADA 2: PILHAS MONTADAS (WALL, ATE 1,8 x 0,75 m, ATE 600 TRIANGULOS)
    // =====================================================================
    // Nas fotos da rodada 1 as salas tinham 14 a 15 props, mas soltos um a um. O conceito tem
    // pilhas encostadas na parede. Cada pilha e um Wall: +Z para fora da parede, que fica em
    // z = -(fundo / 2 + 0,03), o wallGap do DungeonDressing.

    private static GameObject AddNestedProp(Transform parent, string prefabName, Vector3 pos, float escala, Quaternion rot)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsDir}/{prefabName}.prefab");
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = Vector3.one * escala;
        return go;
    }

    private static void BuildPilhaDeCarga(Material matMadeira, Material matTecido, Mesh meshSaco)
    {
        // Wall, 1,8 x 0,75 (as duas cenas): 2 caixas empilhadas, 1 barril, 2 sacos e 1 tabua
        // encostada na parede. Caixa e Barril sao os prefabs que ja existiam. ~364 triangulos.
        GameObject root = new GameObject("PilhaDeCarga");
        AddNestedProp(root.transform, "Caixa", new Vector3(-0.45f, 0f, -0.03f), 1f, Quaternion.Euler(0f, 3f, 0f));
        AddNestedProp(root.transform, "Caixa", new Vector3(-0.40f, 0.70f, -0.06f), 0.72f, Quaternion.Euler(0f, -14f, 0f));
        AddNestedProp(root.transform, "Barril", new Vector3(0.36f, 0f, -0.06f), 1f, Quaternion.identity);
        AddCustomMesh(root.transform, "Saco_1", meshSaco, new Vector3(0.02f, 0f, 0.13f), new Vector3(0.36f, 0.42f, 0.32f), Quaternion.Euler(0f, 25f, 0f), matTecido);
        AddCustomMesh(root.transform, "Saco_2", meshSaco, new Vector3(0.70f, 0f, 0.09f), new Vector3(0.30f, 0.34f, 0.28f), Quaternion.Euler(0f, -30f, 0f), matTecido);
        // Tabua com o topo na parede e o pe 0,3 m pra fora.
        AddPrimitive(root.transform, "Tabua", PrimitiveType.Cube, new Vector3(0.80f, 0.62f, -0.20f), new Vector3(0.18f, 1.30f, 0.04f), Quaternion.Euler(-15f, 0f, 6f), matMadeira);
        SavePropPrefab(root, $"{PropsDir}/PilhaDeCarga.prefab");
    }

    private static void BuildPilhaDeOssos(Material matPedra, Material matOsso, Mesh meshRocha, Mesh meshCranio)
    {
        // Wall, 1,3 x 0,7 (as duas cenas): rocha pequena encostada na parede, cranios e ossos
        // amontoados na frente. ~480 triangulos (o cilindro da Unity tem 80).
        GameObject root = new GameObject("PilhaDeOssos");
        AddCustomMesh(root.transform, "Rocha", meshRocha, new Vector3(-0.22f, -0.02f, -0.04f), new Vector3(0.62f, 0.52f, 0.48f), Quaternion.Euler(0f, 30f, 0f), matPedra);
        AddCustomMesh(root.transform, "Cranio_1", meshCranio, new Vector3(-0.18f, 0.40f, -0.02f), new Vector3(0.18f, 0.18f, 0.20f), Quaternion.Euler(-10f, 20f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_2", meshCranio, new Vector3(0.16f, 0f, 0.06f), new Vector3(0.17f, 0.17f, 0.19f), Quaternion.Euler(0f, -25f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_3", meshCranio, new Vector3(0.36f, 0f, -0.14f), new Vector3(0.16f, 0.16f, 0.18f), Quaternion.Euler(0f, 40f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_4", meshCranio, new Vector3(-0.52f, 0f, 0.06f), new Vector3(0.15f, 0.15f, 0.17f), Quaternion.Euler(0f, 160f, 0f), matOsso);
        AddPrimitive(root.transform, "Osso_1", PrimitiveType.Cylinder, new Vector3(0.05f, 0.02f, 0.12f), new Vector3(0.035f, 0.17f, 0.035f), Quaternion.Euler(90f, 70f, 0f), matOsso);
        AddPrimitive(root.transform, "Osso_2", PrimitiveType.Cylinder, new Vector3(0.46f, 0.02f, 0.04f), new Vector3(0.034f, 0.15f, 0.034f), Quaternion.Euler(90f, -60f, 0f), matOsso);
        AddPrimitive(root.transform, "Osso_3", PrimitiveType.Cylinder, new Vector3(-0.42f, 0.02f, -0.04f), new Vector3(0.032f, 0.14f, 0.032f), Quaternion.Euler(90f, 80f, 0f), matOsso);
        // Osso apoiado na rocha.
        AddPrimitive(root.transform, "Osso_4", PrimitiveType.Cylinder, new Vector3(0.06f, 0.16f, -0.08f), new Vector3(0.034f, 0.16f, 0.034f), Quaternion.Euler(55f, 25f, 0f), matOsso);
        SavePropPrefab(root, $"{PropsDir}/PilhaDeOssos.prefab");
    }

    private static void BuildPilhaEsgoto(Material matMadeira, Material matTecido, Material matPocaVerde, Mesh meshSaco, Mesh meshMancha)
    {
        // Wall, 1,8 x 0,75 (Esgotos): barril tombado, tabuas, saco e uma mancha verde plana
        // embaixo. O barril e o prefab Barril girado 90 graus em z: o pivo dele fica na tampa
        // de baixo, entao o corpo vai do pivo ate 0,85 m para -X. ~350 triangulos.
        GameObject root = new GameObject("PilhaEsgoto");
        AddCustomMesh(root.transform, "ManchaVerde", meshMancha, new Vector3(0.05f, 0f, -0.02f), new Vector3(1.60f, 0.012f, 0.62f), Quaternion.Euler(0f, 4f, 0f), matPocaVerde);
        AddNestedProp(root.transform, "Barril", new Vector3(0.78f, 0.30f, -0.07f), 1f, Quaternion.Euler(0f, 4f, 90f));
        AddPrimitive(root.transform, "Tabua_1", PrimitiveType.Cube, new Vector3(-0.50f, 0.03f, 0.10f), new Vector3(0.80f, 0.04f, 0.15f), Quaternion.Euler(0f, 14f, 0f), matMadeira);
        AddPrimitive(root.transform, "Tabua_2", PrimitiveType.Cube, new Vector3(-0.42f, 0.07f, -0.06f), new Vector3(0.74f, 0.04f, 0.14f), Quaternion.Euler(0f, -9f, 3f), matMadeira);
        AddPrimitive(root.transform, "Tabua_3", PrimitiveType.Cube, new Vector3(-0.78f, 0.55f, -0.24f), new Vector3(0.16f, 1.10f, 0.04f), Quaternion.Euler(-14f, 0f, 6f), matMadeira);
        AddCustomMesh(root.transform, "Saco", meshSaco, new Vector3(-0.08f, 0f, -0.16f), new Vector3(0.38f, 0.40f, 0.34f), Quaternion.Euler(0f, 20f, 0f), matTecido);
        SavePropPrefab(root, $"{PropsDir}/PilhaEsgoto.prefab");
    }

    private static void BuildCantoDoCulto(Material matPedra, Material matOsso, Material matCristal, Mesh meshVela, Mesh meshCranio, Mesh meshCristal)
    {
        // Wall, 1,4 x 0,75 (Profundezas): bloco baixo de pedra com velas e cranios em volta e 2
        // cristais pequenos nas pontas. Vela sem luz, como no Altar. ~330 triangulos.
        GameObject root = new GameObject("CantoDoCulto");
        AddPrimitive(root.transform, "Bloco", PrimitiveType.Cube, new Vector3(0f, 0.20f, -0.08f), new Vector3(0.80f, 0.40f, 0.48f), Quaternion.Euler(0f, 3f, 0f), matPedra);
        AddPrimitive(root.transform, "Tampo", PrimitiveType.Cube, new Vector3(0f, 0.425f, -0.08f), new Vector3(0.88f, 0.05f, 0.54f), Quaternion.Euler(0f, 3f, 0f), matPedra);

        // Velas: x, y, z, altura. Tres no tampo e tres no chao.
        float[,] v =
        {
            { -0.26f, 0.45f, -0.18f, 0.14f }, { 0.02f, 0.45f, -0.22f, 0.18f }, { 0.28f, 0.45f, -0.14f, 0.12f },
            { -0.56f, 0f, 0.10f, 0.16f }, { 0.56f, 0f, 0.14f, 0.10f }, { 0.34f, 0f, 0.22f, 0.13f }
        };
        for (int i = 0; i < v.GetLength(0); i++)
            AddCustomMesh(root.transform, $"Vela_{i + 1}", meshVela, new Vector3(v[i, 0], v[i, 1], v[i, 2]), new Vector3(0.05f, v[i, 3], 0.05f), Quaternion.identity, matOsso);

        AddCustomMesh(root.transform, "Cranio_1", meshCranio, new Vector3(0.10f, 0.45f, 0.02f), new Vector3(0.17f, 0.17f, 0.19f), Quaternion.Euler(0f, -10f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_2", meshCranio, new Vector3(-0.34f, 0f, 0.17f), new Vector3(0.16f, 0.16f, 0.18f), Quaternion.Euler(0f, 30f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cranio_3", meshCranio, new Vector3(0.55f, 0f, -0.16f), new Vector3(0.15f, 0.15f, 0.17f), Quaternion.Euler(0f, -40f, 0f), matOsso);
        AddCustomMesh(root.transform, "Cristal_1", meshCristal, new Vector3(-0.58f, 0f, -0.22f), new Vector3(0.12f, 0.36f, 0.12f), Quaternion.Euler(0f, 20f, 14f), matCristal);
        AddCustomMesh(root.transform, "Cristal_2", meshCristal, new Vector3(0.64f, 0f, -0.26f), new Vector3(0.10f, 0.28f, 0.10f), Quaternion.Euler(-10f, 50f, -12f), matCristal);
        SavePropPrefab(root, $"{PropsDir}/CantoDoCulto.prefab");
    }

    private static void BuildVigaDaPorta(Material matMadeira, Mesh meshViga)
    {
        GameObject root = new GameObject("VigaDaPorta");
        MeshFilter mf = root.AddComponent<MeshFilter>();
        mf.sharedMesh = meshViga;
        MeshRenderer mr = root.AddComponent<MeshRenderer>();
        mr.sharedMaterial = matMadeira;
        SavePropPrefab(root, $"{BlockoutRoot}/VigaDaPorta.prefab");
    }

    private static void AtualizarTochaGuia(Material matFerro, Mesh meshSuporte)
    {
        string path = $"{BlockoutRoot}/TochaGuia.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform existing = root.transform.Find("SuporteDeFerro");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject sup = new GameObject("SuporteDeFerro");
        sup.transform.SetParent(root.transform, false);
        sup.transform.localPosition = Vector3.zero;
        sup.transform.localRotation = Quaternion.identity;
        sup.transform.localScale = Vector3.one;

        MeshFilter mf = sup.AddComponent<MeshFilter>();
        mf.sharedMesh = meshSuporte;
        MeshRenderer mr = sup.AddComponent<MeshRenderer>();
        mr.sharedMaterial = matFerro;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void LigarVigaNoGerador()
    {
        string genPath = "Assets/Scripts/Dungeon/DungeonGenerator.prefab";
        GameObject genRoot = PrefabUtility.LoadPrefabContents(genPath);
        DungeonSeams seams = genRoot.GetComponent<DungeonSeams>();
        if (seams != null)
        {
            SerializedObject so = new SerializedObject(seams);
            SerializedProperty prop = so.FindProperty("lintelPrefab");
            if (prop != null)
            {
                prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{BlockoutRoot}/VigaDaPorta.prefab");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        PrefabUtility.SaveAsPrefabAsset(genRoot, genPath);
        PrefabUtility.UnloadPrefabContents(genRoot);
    }

    // =====================================================================
    // PARTE 1B: ATUALIZAR PROPSETS PELA API (SERIALIZEDOBJECT)
    // =====================================================================

    private struct PropSpec
    {
        public string prefabPath;
        public DungeonPropSet.Placement placement;
        public Vector2 size;
        public Vector2Int perRoom;
        public Vector2Int perCorridor;
        public bool snapRotation;

        public PropSpec(string path, DungeonPropSet.Placement pl, Vector2 sz, Vector2Int pr, Vector2Int pc, bool snap = false)
        {
            prefabPath = path;
            placement = pl;
            size = sz;
            perRoom = pr;
            perCorridor = pc;
            snapRotation = snap;
        }
    }

    public static void AtualizarPropSets()
    {
        // Ordem: grandes primeiro (Rochedo, CantoDeLixo, Altar), mantendo os existentes
        // Calibrado para meta: 10 a 16 props/sala normal e 2 a 5 props/corredor
        // Rodada 2: as pilhas entram depois dos grandes. Caixa, Barril e Sacos soltos ficam em 0
        // na sala, porque agora aparecem dentro da PilhaDeCarga; assim a media fica em 10 a 16.
        PropSpec[] esgotosList =
        {
            // Grandes de canto primeiro
            new PropSpec($"{PropsDir}/Rochedo.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.9f, 0.9f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/CantoDeLixo.prefab", DungeonPropSet.Placement.Corner, new Vector2(1.0f, 1.0f), new Vector2Int(0, 1), new Vector2Int(0, 0)),

            // Rodada 2: pilhas montadas logo depois dos grandes.
            new PropSpec($"{PropsDir}/PilhaDeCarga.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.8f, 0.75f), new Vector2Int(1, 2), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/PilhaDeOssos.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.3f, 0.7f), new Vector2Int(1, 2), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/PilhaEsgoto.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.8f, 0.75f), new Vector2Int(1, 2), new Vector2Int(0, 1)),

            new PropSpec($"{PropsDir}/CaixasEmpilhadas.prefab", DungeonPropSet.Placement.Corner, new Vector2(1.40f, 0.85f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Barris.prefab", DungeonPropSet.Placement.Corner, new Vector2(1.30f, 0.65f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Caixa.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.70f, 0.70f), new Vector2Int(0, 0), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Teia.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.60f, 0.60f), new Vector2Int(1, 2), new Vector2Int(0, 1)),

            // Parede
            new PropSpec($"{PropsDir}/BocaDeEsgoto.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.0f, 0.6f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Pilastra.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.6f, 0.35f), new Vector2Int(1, 3), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/TijolosSoltosEsgotos.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.2f, 0.08f), new Vector2Int(2, 3), new Vector2Int(1, 2)),
            new PropSpec($"{PropsDir}/Sacos.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.9f, 0.5f), new Vector2Int(0, 0), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Correntes.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.5f, 0.15f), new Vector2Int(0, 1), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/Caixa.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.70f, 0.70f), new Vector2Int(0, 0), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Barril.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.60f, 0.60f), new Vector2Int(0, 0), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Entulho.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.90f, 0.60f), new Vector2Int(0, 1), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/Cano.prefab", DungeonPropSet.Placement.Wall, new Vector2(2.50f, 0.30f), new Vector2Int(0, 1), new Vector2Int(0, 0)),

            // Chao
            new PropSpec($"{PropsDir}/LajesEsgotos.prefab", DungeonPropSet.Placement.Floor, new Vector2(3.0f, 3.0f), new Vector2Int(1, 2), new Vector2Int(0, 0), true),
            new PropSpec($"{PropsDir}/Musgo.prefab", DungeonPropSet.Placement.Floor, new Vector2(1.4f, 1.4f), new Vector2Int(1, 2), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/GradeDeDreno.prefab", DungeonPropSet.Placement.Floor, new Vector2(0.9f, 0.9f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Tabuas.prefab", DungeonPropSet.Placement.Floor, new Vector2(1.2f, 0.8f), new Vector2Int(0, 1), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/Ossada.prefab", DungeonPropSet.Placement.Floor, new Vector2(0.8f, 0.6f), new Vector2Int(0, 1), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/PocaEsgoto.prefab", DungeonPropSet.Placement.Floor, new Vector2(1.20f, 0.90f), new Vector2Int(0, 1), new Vector2Int(0, 0))
        };

        PropSpec[] profList =
        {
            // Grandes primeiro
            new PropSpec($"{PropsDir}/Rochedo.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.9f, 0.9f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/CantoDeLixo.prefab", DungeonPropSet.Placement.Corner, new Vector2(1.0f, 1.0f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Altar.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.6f, 0.7f), new Vector2Int(0, 1), new Vector2Int(0, 0)),

            // Rodada 2: pilhas montadas logo depois dos grandes.
            new PropSpec($"{PropsDir}/PilhaDeCarga.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.8f, 0.75f), new Vector2Int(1, 2), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/PilhaDeOssos.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.3f, 0.7f), new Vector2Int(1, 2), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/CantoDoCulto.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.4f, 0.75f), new Vector2Int(1, 2), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/Cristais.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.8f, 0.8f), new Vector2Int(1, 1), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/Cogumelos.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.8f, 0.8f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Cranios.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.8f, 0.8f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Teia.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.6f, 0.6f), new Vector2Int(1, 2), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/Entulho.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.90f, 0.60f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Barril.prefab", DungeonPropSet.Placement.Corner, new Vector2(0.60f, 0.60f), new Vector2Int(0, 0), new Vector2Int(0, 0)),

            // Parede
            new PropSpec($"{PropsDir}/Pilastra.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.6f, 0.35f), new Vector2Int(1, 3), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/TijolosSoltosProfundezas.prefab", DungeonPropSet.Placement.Wall, new Vector2(1.2f, 0.08f), new Vector2Int(2, 3), new Vector2Int(1, 2)),
            new PropSpec($"{PropsDir}/EstandarteCulto.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.8f, 0.15f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Sacos.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.9f, 0.5f), new Vector2Int(0, 0), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/Correntes.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.5f, 0.15f), new Vector2Int(0, 1), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/Entulho.prefab", DungeonPropSet.Placement.Wall, new Vector2(0.90f, 0.60f), new Vector2Int(0, 1), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/Cano.prefab", DungeonPropSet.Placement.Wall, new Vector2(2.50f, 0.30f), new Vector2Int(0, 1), new Vector2Int(0, 0)),

            // Chao
            new PropSpec($"{PropsDir}/SimboloDoCulto.prefab", DungeonPropSet.Placement.Floor, new Vector2(2.4f, 2.4f), new Vector2Int(0, 1), new Vector2Int(0, 0)),
            new PropSpec($"{PropsDir}/LajesProfundezas.prefab", DungeonPropSet.Placement.Floor, new Vector2(3.0f, 3.0f), new Vector2Int(1, 2), new Vector2Int(0, 0), true),
            new PropSpec($"{PropsDir}/Musgo.prefab", DungeonPropSet.Placement.Floor, new Vector2(1.4f, 1.4f), new Vector2Int(1, 2), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/Ossada.prefab", DungeonPropSet.Placement.Floor, new Vector2(0.8f, 0.6f), new Vector2Int(0, 1), new Vector2Int(0, 1)),
            new PropSpec($"{PropsDir}/PocaVerde.prefab", DungeonPropSet.Placement.Floor, new Vector2(1.20f, 0.90f), new Vector2Int(0, 1), new Vector2Int(0, 0))
        };

        ApplyPropSet($"{PropsDir}/PropsEsgotos.asset", esgotosList);
        ApplyPropSet($"{PropsDir}/PropsProfundezas.asset", profList);
    }

    private static void ApplyPropSet(string assetPath, PropSpec[] specs)
    {
        DungeonPropSet asset = AssetDatabase.LoadAssetAtPath<DungeonPropSet>(assetPath);
        if (asset == null) return;

        SerializedObject so = new SerializedObject(asset);
        SerializedProperty entries = so.FindProperty("entries");
        entries.arraySize = specs.Length;

        for (int i = 0; i < specs.Length; i++)
        {
            SerializedProperty el = entries.GetArrayElementAtIndex(i);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(specs[i].prefabPath);
            el.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            el.FindPropertyRelative("placement").enumValueIndex = (int)specs[i].placement;
            el.FindPropertyRelative("size").vector2Value = specs[i].size;
            el.FindPropertyRelative("perRoom").vector2IntValue = specs[i].perRoom;
            el.FindPropertyRelative("perCorridor").vector2IntValue = specs[i].perCorridor;
            el.FindPropertyRelative("snapRotation").boolValue = specs[i].snapRotation;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    // =====================================================================
    // PARTE 3: PREFAB VARIANTS DE SALAS NORMAIS
    // =====================================================================

    private const string ModulosDir = "Assets/Placeholders/Modulos/Prefabs";
    private const string VariantesDir = ModulosDir + "/Variantes";

    public static void GerarVariantesDeSala(Material matMadeira, Material matAgua)
    {
        if (!AssetDatabase.IsValidFolder(VariantesDir))
            AssetDatabase.CreateFolder(ModulosDir, "Variantes");

        DungeonModule vColunas = BuildVariantColunas();
        DungeonModule vCanal = BuildVariantCanal(matMadeira, matAgua);
        DungeonModule vDesabamento = BuildVariantDesabamento();
        DungeonModule vNichos = BuildVariantNichos();

        RegistrarVariantesNoGerador(new[] { vColunas, vCanal, vDesabamento, vNichos });
    }

    private static GameObject PrepareVariantInstance(string basePrefabName)
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModulosDir}/{basePrefabName}.prefab");
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        Transform oldPecas = inst.transform.Find("PecasVariante");
        if (oldPecas != null)
            Object.DestroyImmediate(oldPecas.gameObject);
        return inst;
    }

    private static DungeonModule SaveVariant(GameObject inst, string variantName)
    {
        DungeonModule dm = inst.GetComponent<DungeonModule>();
        SerializedObject so = new SerializedObject(dm);
        so.FindProperty("spawnWeight").floatValue = 2f;
        so.FindProperty("maxInstancesPerDungeon").intValue = 2;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{VariantesDir}/{variantName}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(inst, path);
        Object.DestroyImmediate(inst);
        return saved.GetComponent<DungeonModule>();
    }

    private static GameObject AddRoomPiece(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat, bool addObstacle, List<Bounds> pieceBounds)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        if (addObstacle)
        {
            NavMeshObstacle obs = go.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box;
            obs.carving = true;
            obs.carveOnlyStationary = true;
            obs.size = new Vector3(
                (scale.x + 0.1f) / Mathf.Max(0.01f, scale.x),
                (scale.y + 0.1f) / Mathf.Max(0.01f, scale.y),
                (scale.z + 0.1f) / Mathf.Max(0.01f, scale.z));
        }

        // Approximate local bounds in room root space for spawn point clearance
        Vector3 ext = rot * (scale * 0.5f);
        ext = new Vector3(Mathf.Abs(ext.x), Mathf.Abs(ext.y), Mathf.Abs(ext.z));
        pieceBounds.Add(new Bounds(pos, scale));
        return go;
    }

    private static void RelocateCloseSpawnPoints(Transform roomRoot, List<Bounds> pieceBounds, Vector3[] safePositions)
    {
        int safeIdx = 0;
        foreach (Transform t in roomRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<EnemySpawnPoint>() == null && t.GetComponent<DeadGuardSpawnPoint>() == null)
                continue;

            Vector3 lp = roomRoot.InverseTransformPoint(t.position);
            bool tooClose = false;
            foreach (Bounds b in pieceBounds)
            {
                Vector3 closest = b.ClosestPoint(new Vector3(lp.x, b.center.y, lp.z));
                float dist2D = Vector2.Distance(new Vector2(lp.x, lp.z), new Vector2(closest.x, closest.z));
                if (dist2D < 1.55f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose && safePositions != null && safePositions.Length > 0)
            {
                Vector3 targetLocal = safePositions[safeIdx % safePositions.Length];
                safeIdx++;
                t.position = roomRoot.TransformPoint(targetLocal);
            }
        }
    }

    private static DungeonModule BuildVariantColunas()
    {
        // Room_SmallX1_Colunas: 4 colunas de 0,8 x 0,8 x 3 m em quadrado no meio (conceito 2)
        // Piso do modulo fica em y = 0.125 e topo da parede em y = 3.125
        GameObject inst = PrepareVariantInstance("Room_SmallX1");
        Material roomMat = inst.GetComponent<MeshRenderer>().sharedMaterial;
        Transform grp = new GameObject("PecasVariante").transform;
        grp.SetParent(inst.transform, false);

        List<Bounds> bounds = new List<Bounds>();
        Vector2[] centers =
        {
            new Vector2(-3.0f,  0.5f),
            new Vector2( 3.0f,  0.5f),
            new Vector2(-3.0f, -4.5f),
            new Vector2( 3.0f, -4.5f)
        };
        string[] names = { "NO", "NE", "SO", "SE" };

        for (int i = 0; i < 4; i++)
        {
            float cx = centers[i].x, cz = centers[i].y;
            // Fuste de 0.48 m (evita que DungeonDressing cole Pilastra na coluna) + base e capitel de 0.80 m
            GameObject fuste = AddRoomPiece(grp, $"Coluna_{names[i]}", new Vector3(cx, 1.625f, cz), new Vector3(0.48f, 3.00f, 0.48f), Quaternion.identity, roomMat, true, bounds);
            NavMeshObstacle obs = fuste.GetComponent<NavMeshObstacle>();
            obs.size = new Vector3(0.90f / 0.48f, 3.10f / 3.00f, 0.90f / 0.48f);

            AddRoomPiece(grp, $"BaseColuna_{names[i]}", new Vector3(cx, 0.215f, cz), new Vector3(0.80f, 0.18f, 0.80f), Quaternion.identity, roomMat, false, bounds);
            AddRoomPiece(grp, $"CapitelColuna_{names[i]}", new Vector3(cx, 3.035f, cz), new Vector3(0.80f, 0.18f, 0.80f), Quaternion.identity, roomMat, false, bounds);
        }

        Vector3[] safe =
        {
            new Vector3(0.0f, 0f, -2.0f),
            new Vector3(0.0f, 0f, -6.8f),
            new Vector3(4.3f, 0f, -6.5f),
            new Vector3(-4.2f, 0f, -2.0f)
        };
        RelocateCloseSpawnPoints(inst.transform, bounds, safe);
        return SaveVariant(inst, "Room_SmallX1_Colunas");
    }

    private static DungeonModule BuildVariantCanal(Material matMadeira, Material matAgua)
    {
        // Room_SmallT2_Canal: canal de agua de 1,5 m atravessando a sala, com borda de pedra de 0,2 m,
        // ponte de tabua de 2 m e uma BocaDeEsgoto na parede onde ele nasce (conceito 1).
        // Piso do modulo fica em y = 0.125
        GameObject inst = PrepareVariantInstance("Room_SmallT2");
        Material roomMat = inst.GetComponent<MeshRenderer>().sharedMaterial;
        Transform grp = new GameObject("PecasVariante").transform;
        grp.SetParent(inst.transform, false);

        List<Bounds> bounds = new List<Bounds>();
        float cx = -3.15f; // Canal center X (clears North door at x=0 and connects West door via bridge at z=4)
        float fy = 0.125f;

        // Agua (1,5 m de largura, atravessando de z = -1.70 a z = 9.70, logo acima do piso 0.125)
        AddRoomPiece(grp, "Agua", new Vector3(cx, fy + 0.02f, 4.00f), new Vector3(1.50f, 0.04f, 11.40f), Quaternion.identity, matAgua, false, bounds);

        // Bordas de pedra de 0,2 m de largura e 0,18 m de altura acima do piso (rente ao chao <= 0,2 m)
        AddRoomPiece(grp, "BordaOesteSul",   new Vector3(cx - 0.85f, fy + 0.09f, 0.625f), new Vector3(0.20f, 0.18f, 4.65f), Quaternion.identity, roomMat, false, bounds);
        AddRoomPiece(grp, "BordaLesteSul",   new Vector3(cx + 0.85f, fy + 0.09f, 0.625f), new Vector3(0.20f, 0.18f, 4.65f), Quaternion.identity, roomMat, false, bounds);
        AddRoomPiece(grp, "BordaOesteNorte", new Vector3(cx - 0.85f, fy + 0.09f, 7.375f), new Vector3(0.20f, 0.18f, 4.65f), Quaternion.identity, roomMat, false, bounds);
        AddRoomPiece(grp, "BordaLesteNorte", new Vector3(cx + 0.85f, fy + 0.09f, 7.375f), new Vector3(0.20f, 0.18f, 4.65f), Quaternion.identity, roomMat, false, bounds);

        // Ponte de tabua de 2 m (em z = 3.0..5.0, alinhada com as portas Leste/Oeste em z = 4.0)
        AddRoomPiece(grp, "VigaPonteSul",   new Vector3(cx, fy + 0.06f, 3.15f), new Vector3(2.10f, 0.12f, 0.16f), Quaternion.identity, matMadeira, false, bounds);
        AddRoomPiece(grp, "VigaPonteNorte", new Vector3(cx, fy + 0.06f, 4.85f), new Vector3(2.10f, 0.12f, 0.16f), Quaternion.identity, matMadeira, false, bounds);
        for (int i = 0; i < 6; i++)
        {
            float px = (cx - 0.85f) + i * 0.34f;
            AddRoomPiece(grp, $"TabuaPonte_{i + 1}", new Vector3(px, fy + 0.14f, 4.00f), new Vector3(0.28f, 0.05f, 2.00f), Quaternion.identity, matMadeira, false, bounds);
        }

        // Obstaculos do canal: cobrem a agua e as bordas, MENOS a ponte (z = 2.95..5.05 livre)
        GameObject obsSul = new GameObject("ObstaculoCanalSul");
        obsSul.transform.SetParent(grp, false);
        obsSul.transform.localPosition = new Vector3(cx, 0.5f, 0.625f);
        NavMeshObstacle nmoS = obsSul.AddComponent<NavMeshObstacle>();
        nmoS.shape = NavMeshObstacleShape.Box;
        nmoS.carving = true;
        nmoS.carveOnlyStationary = true;
        nmoS.size = new Vector3(2.00f, 1.0f, 4.75f);

        GameObject obsNorte = new GameObject("ObstaculoCanalNorte");
        obsNorte.transform.SetParent(grp, false);
        obsNorte.transform.localPosition = new Vector3(cx, 0.5f, 7.375f);
        NavMeshObstacle nmoN = obsNorte.AddComponent<NavMeshObstacle>();
        nmoN.shape = NavMeshObstacleShape.Box;
        nmoN.carving = true;
        nmoN.carveOnlyStationary = true;
        nmoN.size = new Vector3(2.00f, 1.0f, 4.75f);

        // BocaDeEsgoto na parede Norte onde o canal nasce (em z = 9.42, olhando para -Z como no Conceito 1)
        GameObject bocaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsDir}/BocaDeEsgoto.prefab");
        if (bocaPrefab != null)
        {
            GameObject boca = (GameObject)PrefabUtility.InstantiatePrefab(bocaPrefab, grp);
            boca.name = "BocaDeEsgoto";
            boca.transform.localPosition = new Vector3(cx, fy + 0.03f, 9.42f);
            boca.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bounds.Add(new Bounds(new Vector3(cx, 0.5f, 9.42f), new Vector3(1.0f, 1.0f, 0.6f)));
        }

        Vector3[] safe =
        {
            new Vector3(1.50f, 0f, 2.00f),
            new Vector3(2.50f, 0f, 5.50f),
            new Vector3(0.50f, 0f, 6.50f),
            new Vector3(3.80f, 0f, 1.50f),
            new Vector3(3.80f, 0f, 7.20f)
        };
        RelocateCloseSpawnPoints(inst.transform, bounds, safe);
        return SaveVariant(inst, "Room_SmallT2_Canal");
    }

    /// <summary>
    /// Rodada 2: as pecas do desabamento ficam com isReadable desligado. O DungeonGuide e o
    /// DungeonDressing tratam como parede toda malha legivel dentro do modulo, e a tocha podia
    /// ir parar numa rocha baixa. Malha nao legivel eles pulam.
    /// </summary>
    private static Mesh SaveNonReadableMesh(string name, Mesh newMesh)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/{name}.asset");
        if (existing != null)
            SetReadable(existing, true);

        Mesh mesh = SaveOrUpdateMesh(name, newMesh);
        SetReadable(mesh, false);
        return mesh;
    }

    private static void SetReadable(Mesh mesh, bool legivel)
    {
        SerializedObject so = new SerializedObject(mesh);
        so.FindProperty("m_IsReadable").boolValue = legivel;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mesh);
    }

    private static GameObject AddPilePiece(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        return AddCustomMesh(parent, name, mesh, pos, scale, rot, mat);
    }

    /// <summary>
    /// Laje caida (caixa com o pivo no centro de baixo): acho a quina mais baixa depois do giro e
    /// subo a peca ate ela ficar 2 cm dentro do piso.
    /// </summary>
    private static void AddFallenSlab(Transform parent, string name, Mesh mesh, float x, float z, float piso, Vector3 scale, Quaternion rot, Material mat)
    {
        float menor = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 quina = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? 0f : 1f, (i & 4) == 0 ? -0.5f : 0.5f);
            menor = Mathf.Min(menor, (rot * Vector3.Scale(quina, scale)).y);
        }
        AddPilePiece(parent, name, mesh, new Vector3(x, piso - 0.02f - menor, z), scale, rot, mat);
    }

    private static DungeonModule BuildVariantDesabamento()
    {
        // Rodada 2: o canto de rocha dos conceitos 1, 2 e 4, no canto noroeste (x de -5,75 a ~-3,0
        // e z de ~6,1 a 9,75), com o piso em y = 0,125. Os 3 BlocoAlto de 3 m na cor da parede
        // sairam: nas fotos ninguem via um desabamento, so parede torta. Agora sao 7 rochas
        // facetadas de PedraDungeon, de 1,8 m junto das paredes a 0,55 m para dentro da sala,
        // 4 lajes caidas e entulho miudo na frente. A rota da L1 vai da porta norte a leste,
        // longe deste canto, entao a borda do NavMesh mais perto dela nao e a da pilha.
        GameObject inst = PrepareVariantInstance("Room_SmallL1");
        Material roomMat = inst.GetComponent<MeshRenderer>().sharedMaterial;
        Material pedra = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/PedraDungeon.mat");
        Transform grp = new GameObject("PecasVariante").transform;
        grp.SetParent(inst.transform, false);

        float fy = 0.125f;
        Mesh[] rochas =
        {
            SaveNonReadableMesh("RochaDesabamento1", BuildNoisyIcosphere(1, 911, 0.22f, new Vector3(1f, 0.9f, 1f))),
            SaveNonReadableMesh("RochaDesabamento2", BuildNoisyIcosphere(1, 912, 0.26f, new Vector3(1f, 1f, 0.9f))),
            SaveNonReadableMesh("RochaDesabamento3", BuildNoisyIcosphere(1, 913, 0.20f, new Vector3(0.9f, 1f, 1f)))
        };
        List<Vector3> bv = new List<Vector3>();
        List<int> bt = new List<int>();
        AddBoxToMesh(bv, bt, new Vector3(0f, 0.5f, 0f), Vector3.one, Quaternion.identity);
        Mesh laje = SaveNonReadableMesh("LajeCaidaDesabamento", FinalizeFacetedMesh(bv, bt, false));
        Mesh miudo = SaveNonReadableMesh("EntulhoDesabamento", BuildNoisyIcosphere(0, 914, 0.25f, Vector3.one));

        // Rochas: x, z, largura, altura, fundo, giro, malha. Afundo 4 cm no piso pra nao flutuar.
        float[,] r =
        {
            { -5.00f, 9.00f, 1.60f, 1.80f, 1.50f,  20f, 0 },
            { -5.15f, 7.70f, 1.30f, 1.45f, 1.30f, -25f, 1 },
            { -3.95f, 9.15f, 1.40f, 1.35f, 1.20f,  55f, 2 },
            { -4.30f, 8.10f, 1.10f, 1.00f, 1.00f,  80f, 1 },
            { -5.20f, 6.80f, 0.90f, 0.75f, 0.90f,  10f, 2 },
            { -3.80f, 7.40f, 0.75f, 0.55f, 0.70f,  35f, 0 },
            { -4.55f, 6.95f, 0.80f, 0.60f, 0.75f, -40f, 1 }
        };
        for (int i = 0; i < r.GetLength(0); i++)
            AddPilePiece(grp, $"Rocha_{i + 1}", rochas[(int)r[i, 6]], new Vector3(r[i, 0], fy - 0.04f, r[i, 1]),
                new Vector3(r[i, 2], r[i, 3], r[i, 4]), Quaternion.Euler(0f, r[i, 5], 0f), pedra);

        // Lajes caidas, inclinadas contra as rochas ou soltas no chao. Subo cada uma ate a quina
        // mais baixa tocar o piso (2 cm dentro); com o pivo no chao, a inclinacao enterrava a
        // outra borda ate 0,19 m.
        AddFallenSlab(grp, "LajeCaida_1", laje, -3.55f, 8.30f, fy, new Vector3(0.85f, 0.12f, 0.65f), Quaternion.Euler(10f, 30f, -16f), pedra);
        AddFallenSlab(grp, "LajeCaida_2", laje, -4.05f, 6.45f, fy, new Vector3(0.90f, 0.12f, 0.70f), Quaternion.Euler(-6f, -20f, 5f), roomMat);
        AddFallenSlab(grp, "LajeCaida_3", laje, -3.30f, 7.05f, fy, new Vector3(0.70f, 0.10f, 0.55f), Quaternion.Euler(4f, 65f, -8f), pedra);
        AddFallenSlab(grp, "LajeCaida_4", laje, -5.25f, 6.15f, fy, new Vector3(0.70f, 0.10f, 0.55f), Quaternion.Euler(-12f, 50f, 8f), roomMat);

        // Entulho miudo na frente: x, z, tamanho.
        float[,] e =
        {
            { -3.25f, 6.55f, 0.22f }, { -3.60f, 6.20f, 0.16f }, { -4.45f, 6.15f, 0.26f },
            { -3.10f, 7.60f, 0.18f }, { -3.20f, 8.05f, 0.24f }, { -4.85f, 6.05f, 0.15f },
            { -3.45f, 9.55f, 0.20f }
        };
        for (int i = 0; i < e.GetLength(0); i++)
            AddPilePiece(grp, $"Entulho_{i + 1}", miudo, new Vector3(e[i, 0], fy - 0.02f, e[i, 1]),
                new Vector3(e[i, 2], e[i, 2] * 0.7f, e[i, 2] * 0.9f), Quaternion.Euler(0f, i * 47f, 0f), i % 3 == 0 ? roomMat : pedra);

        // Um NavMeshObstacle so, cobrindo a pilha toda com 0,1 m a mais.
        Bounds pilha = new Bounds();
        bool primeira = true;
        foreach (Renderer rr in grp.GetComponentsInChildren<Renderer>())
        {
            Bounds b = rr.bounds;
            Bounds local = new Bounds(inst.transform.InverseTransformPoint(b.center), b.size);
            if (primeira) { pilha = local; primeira = false; }
            else pilha.Encapsulate(local);
        }
        GameObject obs = new GameObject("ObstaculoDesabamento");
        obs.transform.SetParent(grp, false);
        obs.transform.localPosition = pilha.center;
        NavMeshObstacle nmo = obs.AddComponent<NavMeshObstacle>();
        nmo.shape = NavMeshObstacleShape.Box;
        nmo.carving = true;
        nmo.carveOnlyStationary = true;
        nmo.size = pilha.size + new Vector3(0.1f, 0.1f, 0.1f);

        List<Bounds> bounds = new List<Bounds> { pilha };
        Vector3[] safe =
        {
            new Vector3(0.00f, 0f, 2.00f),
            new Vector3(2.20f, 0f, 6.20f),
            new Vector3(-2.00f, 0f, 2.50f)
        };
        RelocateCloseSpawnPoints(inst.transform, bounds, safe);
        return SaveVariant(inst, "Room_SmallL1_Desabamento");
    }

    private static DungeonModule BuildVariantNichos()
    {
        // Room_SmallI2_Nichos: 2 pares de tocos de parede (0,25 x 1,5 x 3 m) saindo das paredes compridas, formando nichos
        // Paredes compridas: Oeste (x = -5.75) e Leste (x = +1.75), de z = -1.75 a z = 9.75 (portas em z = 4.0)
        GameObject inst = PrepareVariantInstance("Room_SmallI2");
        Material roomMat = inst.GetComponent<MeshRenderer>().sharedMaterial;
        Transform grp = new GameObject("PecasVariante").transform;
        grp.SetParent(inst.transform, false);

        List<Bounds> bounds = new List<Bounds>();
        Vector3 tocoScale = new Vector3(1.50f, 3.00f, 0.25f);
        float fy = 0.125f;

        AddRoomPiece(grp, "Toco_OesteSul",   new Vector3(-5.00f, fy + 1.50f, -0.35f), tocoScale, Quaternion.identity, roomMat, true, bounds);
        AddRoomPiece(grp, "Toco_LesteSul",   new Vector3( 1.00f, fy + 1.50f, -0.35f), tocoScale, Quaternion.identity, roomMat, true, bounds);
        AddRoomPiece(grp, "Toco_OesteNorte", new Vector3(-5.00f, fy + 1.50f,  8.35f), tocoScale, Quaternion.identity, roomMat, true, bounds);
        AddRoomPiece(grp, "Toco_LesteNorte", new Vector3( 1.00f, fy + 1.50f,  8.35f), tocoScale, Quaternion.identity, roomMat, true, bounds);

        Vector3[] safe =
        {
            new Vector3(-2.00f, 0f, 2.20f),
            new Vector3(-2.00f, 0f, 5.80f),
            new Vector3(-1.20f, 0f, 4.00f),
            new Vector3(-2.80f, 0f, 4.00f),
            new Vector3(-2.00f, 0f, 1.00f)
        };
        RelocateCloseSpawnPoints(inst.transform, bounds, safe);
        return SaveVariant(inst, "Room_SmallI2_Nichos");
    }

    private static void RegistrarVariantesNoGerador(DungeonModule[] variantes)
    {
        string genPath = "Assets/Scripts/Dungeon/DungeonGenerator.prefab";
        GameObject genRoot = PrefabUtility.LoadPrefabContents(genPath);
        ModularDungeonGenerator mg = genRoot.GetComponent<ModularDungeonGenerator>();
        if (mg != null)
        {
            SerializedObject so = new SerializedObject(mg);
            SerializedProperty list = so.FindProperty("roomPrefabs");

            foreach (DungeonModule v in variantes)
            {
                if (v == null) continue;
                bool exists = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == v)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    int idx = list.arraySize;
                    list.InsertArrayElementAtIndex(idx);
                    list.GetArrayElementAtIndex(idx).objectReferenceValue = v;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(genRoot, genPath);
        PrefabUtility.UnloadPrefabContents(genRoot);
    }
}
