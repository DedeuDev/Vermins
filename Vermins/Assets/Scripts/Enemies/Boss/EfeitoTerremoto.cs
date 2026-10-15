using UnityEngine;

public class EfeitoTerremoto : MonoBehaviour
{
    [SerializeField] private float tempoVida = 0.5f;
    [SerializeField] private Vector3 escalaFinal = new Vector3(7f, 0.1f, 7f); // Tamanho da onda de choque

    private float timer = 0f;
    private Vector3 escalaInicial;

    void Start()
    {
        escalaInicial = transform.localScale;
    }

    void Update()
    {
        timer += Time.deltaTime;
        
        // Expande o efeito rapidamente para simular o impacto no chão
        transform.localScale = Vector3.Lerp(escalaInicial, escalaFinal, timer / tempoVida);

        if (timer >= tempoVida)
        {
            Destroy(gameObject);
        }
    }
}