using UnityEngine;

public class FlechaProjetil : MonoBehaviour
{
    [SerializeField] private float velocidade = 15f;
    [SerializeField] private float tempoDeVida = 4f;

    // Ian: dano da flecha no Player. O BossDataSO nao tinha esse numero,
    // entao pus 10 de chute (o inimigo comum tira 12). Ajusta no prefab.
    [SerializeField] private float dano = 10f;

    // Ian: trigger so dispara se um dos dois lados tiver Rigidbody. O
    // Player anda com NavMeshAgent e nao tem, e o prefab da flecha tambem
    // nao tinha, entao o OnTriggerEnter nunca era chamado: a flecha
    // atravessava o Player sem nem escrever no console. Ponho um
    // kinematic aqui, pra nao depender de lembrar de por no prefab.
    void Awake()
    {
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody corpo = gameObject.AddComponent<Rigidbody>();
            corpo.isKinematic = true;
            corpo.useGravity = false;
        }
    }

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

            // Ian: a vida do Player e o Health. Passo a flecha como origem
            // pra o sangue espirrar pro lado contrario de onde ela veio.
            // Rolando ele fica invulneravel, e o Health ja cuida disso.
            var vida = other.GetComponentInParent<Health>();

            if (vida != null)
                vida.TakeDamage(dano, gameObject);

            Destroy(gameObject);
        }
        else if (!EhInimigo(other.transform))
        {
            Destroy(gameObject);
        }
    }

    // Ian: procuro a tag subindo pelos pais. A flecha nasce dentro do
    // collider do Modelo_Ladino, que e filho do boss e esta Untagged: so
    // o pai tem a tag Boss. Olhando so o objeto do collider, a flecha se
    // destruia no mesmo instante em que era disparada.
    private static bool EhInimigo(Transform quem)
    {
        for (Transform t = quem; t != null; t = t.parent)
        {
            if (t.CompareTag("Enemy") || t.CompareTag("Boss"))
                return true;
        }

        return false;
    }
}