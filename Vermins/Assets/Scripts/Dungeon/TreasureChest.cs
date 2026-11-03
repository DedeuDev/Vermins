using System.Collections;
using UnityEngine;

/// <summary>
/// Fiz o bau usar o mesmo clique e a mesma queda dos outros interagiveis.
/// Nao pus Health: abrir tesouro nao e matar nem dar XP ao Player.
/// A tampa ja tem o pivo na borda de tras, no lado +X do bau.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DropOnDeath))]
public class TreasureChest : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform lid;
    [SerializeField] private Light treasureLight;
    [SerializeField] private Vector3 dropOffset = new Vector3(-1.8f, 0f, 0f);
    [SerializeField] private float openingAngle = -100f;
    [SerializeField, Min(0.01f)] private float openingDuration = 0.4f;
    [SerializeField, Min(0.01f)] private float dimDuration = 1f;
    [SerializeField, Range(0f, 1f)] private float openedLightFraction = 0.3f;

    private DropOnDeath dropper;
    private bool opened;

    private void Awake()
    {
        dropper = GetComponent<DropOnDeath>();
    }

    public void Interact()
    {
        if (opened)
            return;

        opened = true;

        // Pus a origem 1,8 m para a frente: com desvio de 0,5 m, o loot
        // fica fora da caixa de 1,2 m e longe da parede de tras (+X).
        dropper.DropAt(transform.TransformPoint(dropOffset));

        // Pus a rotina no dropper, que continua no bau. Assim posso tirar
        // este IInteractable ja no clique: o cursor busca ate componente
        // desligado com GetComponentInParent(true). Mantive o collider
        // solido; so remover o script tira a mao sem mudar o Player.
        dropper.StartCoroutine(Open(lid, treasureLight, openingAngle,
            openingDuration, dimDuration, openedLightFraction));
        Destroy(this);
    }

    private static IEnumerator Open(Transform lid, Light light, float angle,
        float openingTime, float dimTime, float lightFraction)
    {
        Quaternion closedRotation = lid != null ? lid.localRotation : Quaternion.identity;
        Quaternion openRotation = closedRotation * Quaternion.AngleAxis(angle, Vector3.forward);
        float closedIntensity = light != null ? light.intensity : 0f;
        float duration = Mathf.Max(openingTime, dimTime);
        float elapsed = 0f;

        // Usei tempo do jogo: pausar segura a tampa junto com o Player.
        // So esta abertura roda por frame, por no maximo 1 s no prefab.
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            if (lid != null)
                lid.localRotation = Quaternion.Slerp(closedRotation, openRotation,
                    Mathf.SmoothStep(0f, 1f, elapsed / openingTime));
            if (light != null)
                light.intensity = Mathf.Lerp(closedIntensity, closedIntensity * lightFraction,
                    Mathf.Clamp01(elapsed / dimTime));
            yield return null;
        }
    }
}
