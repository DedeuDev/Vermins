using TMPro;
using UnityEngine;

/// <summary>
/// Base de tudo que fica no chao e o jogador pega encostando: ouro, joia,
/// pocao. Quem herda so diz o que entregar e pra quem, no Entregar.
///
/// Fiz depois do terceiro coletavel. Os tres repetiam a mesma montagem e
/// a mesma trava contra entrega dupla, e o ContextualCursor tinha que
/// listar um por um. Pickup novo herda daqui e ja ganha tudo isso.
///
/// O Rigidbody kinematic fica aqui, e nao no jogador. Trigger so dispara
/// se um dos dois lados tiver Rigidbody, e o NavMeshAgent do jogador nao
/// conta como um. Rigidbody no jogador faria a fisica brigar com o agent
/// pela posicao dele. E a mesma regra da flecha do boss.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public abstract class Pickup : MonoBehaviour
{
    // Onde o nome fica, acima do pivo do item, e o tamanho da letra no
    // mundo. Os itens sao todos do mesmo porte, entao vale pra todos.
    private const float AlturaDoRotulo = 1.0f;
    private const float TamanhoDoRotulo = 3f;

    // Um material so pra todos os rotulos. Contorno no TextMeshPro pela
    // propriedade outlineWidth cria um material por texto; com uma horda
    // soltando item isso vira dezenas.
    private static Material materialDoRotulo;

    private bool coletado;

    /// <summary>
    /// Se tenta de novo enquanto quem nao pode levar continua em cima. A
    /// pocao liga: com o cinto cheio ela fica no chao, e se o jogador
    /// beber sem sair de cima ela tem que entrar. Ouro e joia nao
    /// precisam, porque quem tem carteira sempre leva na primeira.
    /// </summary>
    protected virtual bool TentarDeNovoEmCima => false;

    /// <summary>
    /// O que aparece escrito em cima do item no chao. Lido uma vez, no
    /// Start, entao a quantia ja sorteada no Awake entra no texto.
    /// </summary>
    protected abstract string TextoDoRotulo { get; }

    /// <summary>A cor do nome. Cada tipo de item tem a sua.</summary>
    protected virtual Color CorDoRotulo => Color.white;

    /// <summary>
    /// Escreve o nome em cima do item, como os dois jogos fazem com o que
    /// cai no chao: da pra saber o que caiu sem passar o mouse em cada
    /// coisa. O texto e montado uma vez so e nao muda.
    /// </summary>
    protected virtual void Start()
    {
        var objeto = new GameObject("Rotulo");
        objeto.transform.SetParent(transform, false);
        objeto.transform.localPosition = Vector3.up * AlturaDoRotulo;

        // A camera nunca gira, entao basta copiar a rotacao dela uma vez
        // pro texto ficar de frente. Mesmo jeito do DamageNumbers.
        Camera cam = Camera.main;

        if (cam != null)
            objeto.transform.rotation = cam.transform.rotation;

        var rotulo = objeto.AddComponent<TextMeshPro>();
        rotulo.text = TextoDoRotulo;
        rotulo.fontSize = TamanhoDoRotulo;
        rotulo.color = CorDoRotulo;
        rotulo.alignment = TextAlignmentOptions.Center;
        rotulo.fontStyle = FontStyles.Bold;
        rotulo.textWrappingMode = TextWrappingModes.NoWrap;
        rotulo.rectTransform.sizeDelta = new Vector2(4f, 1f);

        if (materialDoRotulo == null)
        {
            // Contorno escuro pra ler no chao claro da cidade e no esgoto.
            materialDoRotulo = new Material(rotulo.fontSharedMaterial);
            materialDoRotulo.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            materialDoRotulo.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(20, 12, 8, 255));
        }

        rotulo.fontSharedMaterial = materialDoRotulo;
    }

    // O Reset roda quando o componente e adicionado no editor. Deixo o
    // Rigidbody e a esfera prontos aqui pra ninguem precisar lembrar da
    // regra do trigger: e so adicionar o script.
    protected virtual void Reset()
    {
        Rigidbody corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;

        // O collider do jogador vai de perto do chao ate 2 m, com 0,5 de
        // raio. Com a esfera a meio metro do chao e 0,6 de raio, ele pega
        // o item quando passa a menos de 1,1 m dele.
        SphereCollider area = GetComponent<SphereCollider>();
        area.isTrigger = true;
        area.center = new Vector3(0f, 0.5f, 0f);
        area.radius = 0.6f;
    }

    private void OnTriggerEnter(Collider other)
    {
        TentarColetar(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (TentarDeNovoEmCima)
            TentarColetar(other);
    }

    private void TentarColetar(Collider other)
    {
        // O trigger avisa uma vez por collider que entra, e o Destroy so
        // tira o item do jogo no fim do frame. Hoje o Player tem um
        // collider so e nao acontece nada. Com um segundo collider nele,
        // medi 20 itens entregando em dobro: 212 de ouro em vez de 106, e
        // 10 joias virando 20.
        if (coletado)
            return;

        // Marco antes de entregar, pra uma reentrada pelo OnChanged de
        // quem recebe tambem nao pegar o mesmo item de novo.
        coletado = true;

        if (!Entregar(other))
        {
            coletado = false;
            return;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Entrega o item pra quem encostou. Devolve false se esse collider
    /// nao leva (nao tem carteira, cinto cheio), e ai o item fica no
    /// chao. Procure com GetComponentInParent: o collider costuma estar
    /// num filho e o resto no objeto raiz.
    /// </summary>
    protected abstract bool Entregar(Collider quemEncostou);
}
