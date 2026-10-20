using UnityEngine;
using UnityEngine.Serialization;

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
public class DamageFlash : MonoBehaviour
{
    [FormerlySerializedAs("cor")]
    [SerializeField] private Color color = Color.red;

    [Tooltip("Quanto tempo o flash leva pra apagar. Curto de proposito: " +
             "o inimigo bate a cada 1,5 s e o jogador a cada 1,2 s, entao " +
             "qualquer coisa acima de uns 0,3 s faria um golpe emendar no " +
             "outro e viraria uma luz acesa em vez de um pisca por golpe.")]
    [FormerlySerializedAs("duracao")]
    [SerializeField] private float duration = 0.15f;

    [Tooltip("Quanto da cor entra no auge do flash. 1 apaga a textura " +
             "inteira e o personagem vira uma silhueta vermelha; deixei " +
             "menos pra ainda dar pra ver quem e que levou.")]
    [Range(0f, 1f)]
    [FormerlySerializedAs("forca")]
    [SerializeField] private float strength = 0.8f;

    // O _BaseColor e o nome no URP/Lit. O _Color e o nome antigo, que
    // alguns shaders ainda usam - procuro os dois pra isto nao morrer
    // calado se alguem trocar o material do inimigo.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Health health;
    private Renderer[] renderers;
    private Color[] originalColors;
    private int[] colorPropertyIds;
    private MaterialPropertyBlock propertyBlock;

    private float remaining;

    private void Awake()
    {
        health = GetComponent<Health>();
        renderers = GetComponentsInChildren<Renderer>(true);

        originalColors = new Color[renderers.Length];
        colorPropertyIds = new int[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material m = renderers[i].sharedMaterial;

            colorPropertyIds[i] =
                m != null && m.HasProperty(BaseColorId) ? BaseColorId :
                m != null && m.HasProperty(ColorId) ? ColorId : 0;

            originalColors[i] = colorPropertyIds[i] != 0 ? m.GetColor(colorPropertyIds[i]) : Color.white;
        }

        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        health.OnDamaged -= HandleDamaged;

        // Se o componente for desligado no meio de um flash, o corpo
        // ficaria vermelho pra sempre.
        remaining = 0f;
        ApplyTint(0f);
    }

    private void HandleDamaged(float dano, GameObject quemBateu)
    {
        // Reinicio em vez de somar. Dois golpes juntos tem que dar dois
        // piscas curtos, nao um vermelho que vai ficando.
        remaining = duration;
    }

    private void Update()
    {
        if (remaining <= 0f)
            return;

        remaining -= Time.deltaTime;

        ApplyTint(remaining > 0f ? remaining / duration : 0f);
    }

    /// <summary>
    /// Mistura a cor do flash na cor original. Uso MaterialPropertyBlock e
    /// nao renderer.material de proposito: os tres inimigos da cena
    /// dividem o MESMO material, entao mexer no material faria os tres
    /// piscarem juntos - e ler renderer.material criaria uma copia por
    /// inimigo, que vaza toda vez que a cena roda.
    /// </summary>
    private void ApplyTint(float intensidade)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || colorPropertyIds[i] == 0)
                continue;

            renderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(colorPropertyIds[i],
                Color.Lerp(originalColors[i], color, intensidade * strength));
            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }
}
