using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Liga os sliders de volume da tela de Options ao VolumeSettings.
/// Cada linha tem o canal do mixer, o slider (0 a 1) e um texto opcional com a porcentagem.
/// </summary>
public class VolumeSliders : MonoBehaviour
{
    [Serializable]
    public struct Row
    {
        public AudioChannel channel;
        public Slider slider;
        public TMP_Text valueLabel;
    }

    [SerializeField] private Row[] rows = Array.Empty<Row>();

    // Guardo os listeners pra conseguir tirar no OnDisable; sem isso, abrir a
    // tela de novo somaria um listener a mais em cada slider.
    private UnityAction<float>[] listeners;

    private void OnEnable()
    {
        if (listeners == null || listeners.Length != rows.Length)
            listeners = new UnityAction<float>[rows.Length];

        for (int i = 0; i < rows.Length; i++)
        {
            Row row = rows[i];
            if (row.slider == null)
                continue;

            float saved = VolumeSettings.GetVolume(row.channel);

            // SetValueWithoutNotify pra nao regravar o PlayerPrefs so por abrir a tela.
            row.slider.SetValueWithoutNotify(saved);
            UpdateLabel(row.valueLabel, saved);

            if (listeners[i] == null)
            {
                AudioChannel channel = row.channel;
                TMP_Text label = row.valueLabel;
                listeners[i] = value =>
                {
                    VolumeSettings.SetVolume(channel, value);
                    UpdateLabel(label, value);
                };
            }

            row.slider.onValueChanged.AddListener(listeners[i]);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i].slider != null && listeners != null && listeners[i] != null)
                rows[i].slider.onValueChanged.RemoveListener(listeners[i]);
        }

        // O VolumeSettings nao grava a cada arrasto; gravo aqui, quando a tela
        // fecha, pra o volume nao se perder se o jogo cair antes de sair direito.
        PlayerPrefs.Save();
    }

    private static void UpdateLabel(TMP_Text label, float value)
    {
        if (label == null)
            return;

        // SetText com argumento nao monta string nova a cada arrasto.
        label.SetText("{0}%", Mathf.RoundToInt(value * 100f));
    }
}
