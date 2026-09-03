using UnityEngine;

/// <summary>
/// Pisca o corpo de vermelho toda vez que leva dano.
///
/// Serve pra enxergar o combate acontecendo. Hoje o dano e um numero que
/// so existe no console: da pra estar batendo, errando ou batendo duas
/// vezes no mesmo frame e a tela fica igual. Com o flash da pra ver na
/// hora quem levou e quantas vezes.
///
/// Vai em qualquer coisa que tenha Health - jogador, inimigo, barril -
/// porque o Health tambem e generico. Quem bate nao pisca; pisca quem
/// leva. Como o jogador e o inimigo se batem, os dois acabam piscando.
///
/// E ferramenta de debug, nao efeito final: quando entrar VFX de impacto
/// de verdade, isto aqui sai ou vira so o complemento dele.
/// </summary>
[RequireComponent(typeof(Health))]
[DisallowMultipleComponent]
public class FlashDeDano : MonoBehaviour
{
    [SerializeField] private Color cor = Color.red;

    [Tooltip("Quanto tempo o flash leva pra apagar. Curto de proposito: " +
             "o inimigo bate a cada 1,5 s e o jogador a cada 1,2 s, entao " +
             "qualquer coisa acima de uns 0,3 s faria um golpe emendar no " +
             "outro e viraria uma luz acesa em vez de um pisca por golpe.")]
    [SerializeField] private float duracao = 0.15f;

    [Tooltip("Quanto da cor entra no auge do flash. 1 apaga a textura " +
             "inteira e o personagem vira uma silhueta vermelha; deixei " +
             "menos pra ainda dar pra ver quem e que levou.")]
    [Range(0f, 1f)]
    [SerializeField] private float forca = 0.8f;

    // O _BaseColor e o nome no URP/Lit. O _Color e o nome antigo, que
    // alguns shaders ainda usam - procuro os dois pra isto nao morrer
    // calado se alguem trocar o material do inimigo.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Health vida;
    private Renderer[] corpos;
    private Color[] originais;
    private int[] propriedades;
    private MaterialPropertyBlock bloco;

    private float restante;

    private void Awake()
    {
        vida = GetComponent<Health>();
        corpos = GetComponentsInChildren<Renderer>(true);

        originais = new Color[corpos.Length];
        propriedades = new int[corpos.Length];

        for (int i = 0; i < corpos.Length; i++)
        {
            Material m = corpos[i].sharedMaterial;

            propriedades[i] =
                m != null && m.HasProperty(BaseColorId) ? BaseColorId :
                m != null && m.HasProperty(ColorId) ? ColorId : 0;

            originais[i] = propriedades[i] != 0 ? m.GetColor(propriedades[i]) : Color.white;
        }

        bloco = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        vida.OnDamaged += Piscar;
    }

    private void OnDisable()
    {
        vida.OnDamaged -= Piscar;

        // Se o componente for desligado no meio de um flash, o corpo
        // ficaria vermelho pra sempre.
        restante = 0f;
        Pintar(0f);
    }

    private void Piscar(float dano, GameObject quemBateu)
    {
        // Reinicio em vez de somar. Dois golpes juntos tem que dar dois
        // piscas curtos, nao um vermelho que vai ficando.
        restante = duracao;
    }

    private void Update()
    {
        if (restante <= 0f)
            return;

        restante -= Time.deltaTime;

        Pintar(restante > 0f ? restante / duracao : 0f);
    }

    /// <summary>
    /// Mistura a cor do flash na cor original. Uso MaterialPropertyBlock e
    /// nao renderer.material de proposito: os tres inimigos da cena
    /// dividem o MESMO material, entao mexer no material faria os tres
    /// piscarem juntos - e ler renderer.material criaria uma copia por
    /// inimigo, que vaza toda vez que a cena roda.
    /// </summary>
    private void Pintar(float intensidade)
    {
        for (int i = 0; i < corpos.Length; i++)
        {
            if (corpos[i] == null || propriedades[i] == 0)
                continue;

            corpos[i].GetPropertyBlock(bloco);
            bloco.SetColor(propriedades[i],
                Color.Lerp(originais[i], cor, intensidade * forca));
            corpos[i].SetPropertyBlock(bloco);
        }
    }
}
