using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Mede um clipe de animacao do jeito que importa pro jogo: pra onde o
/// corpo aponta e a que velocidade o pe anda.
///
/// Existe porque os dois numeros que o Unity da de graca nao servem aqui.
/// O clip.averageSpeed sai do root motion, e nos clipes deste pack o root
/// motion esta ~36 graus fora da direcao pra qual o corpo aponta - medi
/// isso comparando a linha dos ombros com a direcao do deslocamento. Um
/// dos dois esta errado, e e o root motion: quando eu alinho pelo corpo,
/// os ombros ficam em +8,5 graus e o quadril em -8,6, simetricos em torno
/// do zero, que e a contra-rotacao normal de quem anda. Alinhando pelo
/// root motion os dois ficam positivos (+47 e +30), que e corpo torto com
/// a contra-rotacao por cima.
///
/// Entao aqui eu amostro o clipe quadro a quadro e tiro:
///   - o angulo do corpo, pela linha dos ombros e dos quadris;
///   - a velocidade, pelo pe que esta plantado no chao.
///
/// Amostrar exige o modelo montado numa cena. Uso uma cena de preview pra
/// nao sujar a cena aberta - ja quebrei isso uma vez neste projeto.
/// </summary>
public sealed class AnaliseDeClipe : IDisposable
{
    public struct Medida
    {
        public bool valido;

        /// <summary>
        /// Quantos graus o corpo esta girado em relacao ao forward do
        /// personagem, na media do ciclo. Positivo e pra direita.
        /// </summary>
        public float anguloDoCorpo;

        /// <summary>
        /// Velocidade em m/s tirada do pe que esta no chao. Este e o
        /// numero que o NavMeshAgent tem que andar pro pe nao deslizar.
        /// </summary>
        public float velocidade;

        /// <summary>
        /// Pra onde o pe varre o chao, em graus, visto de dentro do corpo.
        /// 180 e varrer pra tras, que e o certo pra quem anda pra frente;
        /// 90 e varrer pra direita, que e o certo pra quem anda pra
        /// esquerda. Este e o numero que diz se o clipe esta torto ou nao -
        /// mais confiavel que o angulo do tronco e mais confiavel que o
        /// root motion.
        /// </summary>
        public float direcaoDaPassada;

        /// <summary>Quantos pes deram passada legivel (0, 1 ou 2).</summary>
        public int amostrasNoChao;
    }

    /// <summary>
    /// Fracao da altura do passo abaixo da qual eu considero o pe
    /// apoiado. Com a medida por passada o numero final quase nao
    /// depende deste valor - foi assim que eu vi que a medida antiga
    /// estava errada.
    /// </summary>
    private const float LimiarDeApoio = 0.25f;

    private const string ModeloPath = "Assets/Placeholders/Player/PlayerTest.fbx";

    private readonly UnityEngine.SceneManagement.Scene cena;
    private readonly GameObject instancia;
    private readonly Animator animator;

    private readonly Transform ombroE, ombroD, quadrilE, quadrilD, peE, peD;

    public bool Pronto => animator != null && ombroE != null && peE != null;

    public AnaliseDeClipe()
    {
        cena = EditorSceneManager.NewPreviewScene();

        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloPath);

        if (modelo == null)
            return;

        instancia = UnityEngine.Object.Instantiate(modelo);
        instancia.hideFlags = HideFlags.HideAndDontSave;
        EditorSceneManager.MoveGameObjectToScene(instancia, cena);

        animator = instancia.GetComponent<Animator>();

        if (animator == null)
        {
            animator = instancia.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModeloPath);
        }

        ombroE = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        ombroD = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        quadrilE = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        quadrilD = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        peE = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        peD = animator.GetBoneTransform(HumanBodyBones.RightFoot);
    }

    public Medida Medir(AnimationClip clipe, int amostras = 72)
    {
        var m = new Medida();

        if (!Pronto || clipe == null || clipe.length <= 0f || amostras < 8)
            return m;

        Transform raiz = instancia.transform;

        var dir = new Vector2[amostras];
        var pesE = new Vector3[amostras];
        var pesD = new Vector3[amostras];

        float passo = clipe.length / amostras;

        AnimationMode.StartAnimationMode();

        try
        {
            for (int i = 0; i < amostras; i++)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(instancia, clipe, i * passo);
                AnimationMode.EndSampling();

                // Somo ombros e quadris como VETORES e nao como angulos.
                // Media de angulo perto de +-180 da numero sem sentido, e
                // somando os dois de uma vez a contra-rotacao entre eles
                // se cancela sozinha.
                dir[i] = Direcao(ombroE, ombroD, raiz) + Direcao(quadrilE, quadrilD, raiz);

                // Pes em espaco do corpo. Se a amostragem aplicar root
                // motion, o InverseTransformPoint desconta; se nao
                // aplicar, o passo ja esta na pose. Certo dos dois jeitos.
                pesE[i] = raiz.InverseTransformPoint(peE.position);
                pesD[i] = raiz.InverseTransformPoint(peD.position);
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }

        Vector2 soma = Vector2.zero;

        foreach (Vector2 d in dir)
            soma += d;

        // A linha esquerda->direita do corpo aponta pro lado quando o
        // personagem esta reto. O angulo dela ate o eixo X e o quanto o
        // corpo esta girado.
        m.anguloDoCorpo = soma.sqrMagnitude > 1e-6f
            ? Vector2.SignedAngle(Vector2.right, soma.normalized)
            : 0f;

        // Sinal: o angulo assinado no plano XZ cresce no sentido
        // anti-horario visto de cima, e o yaw do Unity cresce no horario.
        m.anguloDoCorpo = -m.anguloDoCorpo;

        Vector2 passada = Vector2.zero;
        float tempo = 0f;
        int n = 0;

        MedirPe(pesE, passo, clipe.isLooping, ref passada, ref tempo, ref n);
        MedirPe(pesD, passo, clipe.isLooping, ref passada, ref tempo, ref n);

        m.amostrasNoChao = n;
        m.velocidade = tempo > 0f ? passada.magnitude / tempo : 0f;
        m.direcaoDaPassada = n > 0 && passada.sqrMagnitude > 1e-6f
            ? Mathf.Atan2(passada.x, passada.y) * Mathf.Rad2Deg
            : 0f;
        m.valido = true;

        return m;
    }

    /// <summary>
    /// Velocidade do pe pela passada, nao amostra a amostra.
    ///
    /// Primeiro tentei somar o deslocamento de um quadro pro outro
    /// enquanto o pe estava baixo, e nao converge: mexendo o limiar de
    /// apoio de 33% pra 15% da altura, a velocidade do sprint pulava de
    /// 4,50 pra 5,77. O motivo e que o tornozelo acelera na batida do
    /// calcanhar e na saida da ponta, entao cada amostra solta carrega
    /// ruido, e somar todas acumula esse ruido.
    ///
    /// Aqui eu pego o maior trecho CONTINUO em que o pe fica baixo, olho
    /// so o deslocamento liquido da ponta a ponta desse trecho e divido
    /// pela duracao dele. Isso e a passada, e nao depende de quantas
    /// amostras cairam dentro nem de quanto o tornozelo balancou no meio.
    /// </summary>
    private static void MedirPe(Vector3[] pos, float passo, bool ciclo, ref Vector2 passada, ref float tempo, ref int n)
    {
        float baixo = float.MaxValue, alto = float.MinValue;

        foreach (Vector3 p in pos)
        {
            if (p.y < baixo) baixo = p.y;
            if (p.y > alto) alto = p.y;
        }

        float limite = baixo + (alto - baixo) * LimiarDeApoio;

        int melhorInicio = -1, melhorTamanho = 0;
        int inicio = -1, tamanho = 0;

        // Dou duas voltas no array quando o clipe e ciclo, pra achar
        // tambem o apoio que comeca no fim e termina no comeco.
        int voltas = ciclo ? pos.Length * 2 : pos.Length;

        for (int k = 0; k < voltas; k++)
        {
            if (pos[k % pos.Length].y <= limite)
            {
                if (inicio < 0) { inicio = k; tamanho = 0; }
                tamanho++;

                if (tamanho > melhorTamanho && tamanho <= pos.Length)
                {
                    melhorTamanho = tamanho;
                    melhorInicio = inicio;
                }
            }
            else
            {
                inicio = -1;
                tamanho = 0;
            }
        }

        if (melhorTamanho < 2)
            return;

        Vector3 a = pos[melhorInicio % pos.Length];
        Vector3 b = pos[(melhorInicio + melhorTamanho - 1) % pos.Length];

        Vector3 d = b - a;

        passada += new Vector2(d.x, d.z);
        tempo += (melhorTamanho - 1) * passo;
        n++;
    }

    private static Vector2 Direcao(Transform e, Transform d, Transform raiz)
    {
        if (e == null || d == null)
            return Vector2.zero;

        Vector3 v = raiz.InverseTransformPoint(d.position) - raiz.InverseTransformPoint(e.position);

        return new Vector2(v.x, v.z).normalized;
    }

    public void Dispose()
    {
        if (instancia != null)
            UnityEngine.Object.DestroyImmediate(instancia);

        if (cena.IsValid())
            EditorSceneManager.ClosePreviewScene(cena);
    }
}
