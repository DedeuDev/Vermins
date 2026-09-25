using UnityEngine;

public class PocaVeneno : MonoBehaviour
{
    [SerializeField] private float duracaoPoca = 5f;     // Tempo que a poça fica no chão
    [SerializeField] private float danoPorSegundo = 5f;  // Dano contínuo
    [SerializeField] private float raioArea = 2.5f;       // Tamanho visual/collider

    private float timerDano;

    void Start()
    {
        // Destrói a poça após a duração configurada
        Destroy(gameObject, duracaoPoca);
    }

    private void OnTriggerStay(Collider other)
    {
        // Aplica dano contínuo enquanto o Player estiver sobre a poça
        if (other.CompareTag("Player"))
        {
            timerDano += Time.deltaTime;
            
            if (timerDano >= 1.0f) // A cada 1 segundo
            {
                timerDano = 0f;
                Debug.Log("Player está recebendo dano de VENENO!");

                /* 
                // Se tiver o script de vida do Player, descomente abaixo:
                var playerHealth = other.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TomarDano(danoPorSegundo);
                }
                */
            }
        }
    }
}