using UnityEngine;

/// <summary>
/// Cues opcionais de dano e morte pra um inimigo ou boss especifico.
/// Quando vazio, o AudioManager usa enemyHurt e enemyDeath da AudioLibrary.
///
/// Pus separado pra nao precisar editar nenhum prefab de colega: quem
/// quiser trocar o som de um bicho so adiciona este script nele.
/// </summary>
[DisallowMultipleComponent]
public class HealthAudio : MonoBehaviour
{
    [SerializeField] private SoundCue hurtCue;
    [SerializeField] private SoundCue deathCue;

    public SoundCue HurtCue => hurtCue;
    public SoundCue DeathCue => deathCue;
}
