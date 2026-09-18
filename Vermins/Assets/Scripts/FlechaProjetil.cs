using UnityEngine;

public class FlechaProjetil : MonoBehaviour
{
    [SerializeField] private float velocidade = 15f;
    [SerializeField] private float tempoDeVida = 4f;
    [SerializeField] private float dano = 15f;

    void Start()
    {
        // Destrói a flecha após 4 segundos para não poluir a cena
        Destroy(gameObject, tempoDeVida);
    }

    void Update()
    {
        // Move a flecha continuamente para a frente na direção em que foi disparada
        transform.Translate(Vector3.forward * velocidade * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Se acertar o Player
        if (other.CompareTag("Player"))
        {
            // Tenta dar dano se o player tiver o componente de Vida/Stats
            /*
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null) stats.TomarDano(dano);
            */
            
            Debug.Log("Flecha acertou o Player!");
            Destroy(gameObject);
        }
        else if (!other.CompareTag("Enemy") && !other.CompareTag("Boss"))
        {
            // Se acertar uma parede ou chão (que não seja o próprio boss)
            Destroy(gameObject);
        }
    }
}