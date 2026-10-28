using TMPro;
using UnityEngine;

[RequireComponent(typeof(Health))]
public sealed class SandboxTrainingDummy : MonoBehaviour
{
    [SerializeField] private float resetDelay = 3f;
    [SerializeField] private TMP_Text label;
    private Health health;
    private float lastDamageTime;
    private bool damaged;
    private float lastDamage;
    private string title;

    private void Awake()
    {
        health = GetComponent<Health>();
        title = gameObject.name;
    }

    private void OnEnable()
    {
        health.OnDamaged += HandleDamage;
        health.OnChanged += HandleChanged;
    }

    private void Start()
    {
        if (label != null && Camera.main != null)
            label.transform.rotation = Camera.main.transform.rotation;
        RefreshLabel();
    }

    private void HandleDamage(float amount, GameObject source)
    {
        lastDamage = amount;
        lastDamageTime = Time.time;
        damaged = true;
    }

    private void HandleChanged(float current, float maximum)
    {
        RefreshLabel();
    }

    private void Update()
    {
        if (!damaged || Time.time - lastDamageTime < resetDelay) return;
        damaged = false;
        health.Revive();
    }

    private void RefreshLabel()
    {
        // Atualizo a medida por evento, sem criar texto a cada frame.
        if (label != null)
            label.text = title + "\n" + health.Current.ToString("0.#") + "/" +
                health.Max.ToString("0") + "  golpe " + lastDamage.ToString("0.#");
    }

    private void OnDisable()
    {
        health.OnDamaged -= HandleDamage;
        health.OnChanged -= HandleChanged;
    }
}
