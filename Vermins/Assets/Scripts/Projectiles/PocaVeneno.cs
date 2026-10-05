using UnityEngine;

public class PocaVeneno : MonoBehaviour
{
    [SerializeField] private float duracaoPoca = 5f;     // Tempo que a poça fica no chão
    [SerializeField] private float danoPorSegundo = 5f;  // Dano contínuo
    [SerializeField] private float raioArea = 2.5f;       // Tamanho visual/collider

    private float timerDano;

    // Ian: mesmo caso da flecha. Sem Rigidbody na poca nem no Player, o
    // OnTriggerStay nunca era chamado. O sleepThreshold em zero impede
    // o corpo de dormir: dormindo, o Stay para de chegar com o Player
    // parado em cima da poca.
    void Awake()
    {
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody corpo = gameObject.AddComponent<Rigidbody>();
            corpo.isKinematic = true;
            corpo.useGravity = false;
            corpo.sleepThreshold = 0f;
        }
    }

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

                // Ian: o bloco comentado aqui procurava um PlayerHealth, que
                // nao existe. A vida do Player e o Health. Sem origem, o
                // sangue sai pra cima em vez de pro lado.
                var vida = other.GetComponentInParent<Health>();

                if (vida != null)
                    vida.TakeDamage(danoPorSegundo);
            }
        }
    }
}