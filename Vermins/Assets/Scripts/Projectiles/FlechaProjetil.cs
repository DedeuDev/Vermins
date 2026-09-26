using UnityEngine;

public class FlechaProjetil : MonoBehaviour
{
    [SerializeField] private float velocidade = 15f;
    [SerializeField] private float tempoDeVida = 4f;

    void Start()
    {
        // Garante que a flecha seja um objeto solto no mundo (sem pai)
        transform.SetParent(null);
        
        Destroy(gameObject, tempoDeVida);
    }

    void Update()
    {
        // Apenas move para FRENTE no eixo local. 
        // Não altera a rotação em NENHUM momento no Update!
        transform.Translate(Vector3.forward * velocidade * Time.deltaTime, Space.Self);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Flecha acertou o Player!");
            Destroy(gameObject);
        }
        else if (!other.CompareTag("Enemy") && !other.CompareTag("Boss"))
        {
            Destroy(gameObject);
        }
    }
}