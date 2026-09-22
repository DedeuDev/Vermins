using UnityEngine;

/// <summary>
/// Pocao no chao. Quem encosta e tem cinto leva ela.
///
/// Filtro por "tem PotionBelt", e nao pela tag Player. Inimigo tambem
/// passa por cima da pocao, e nao ter cinto ja separa um do outro.
///
/// O Rigidbody kinematic fica aqui, e nao no jogador. Trigger so
/// dispara se um dos dois lados tiver Rigidbody, e o NavMeshAgent do
/// jogador nao conta como um. Rigidbody no jogador faria a fisica
/// brigar com o agent pela posicao dele. E a mesma regra da flecha do
/// boss.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class PotionPickup : MonoBehaviour
{
    // O Reset roda quando o componente e adicionado no editor. Deixo o
    // Rigidbody e a esfera prontos aqui pra ninguem precisar lembrar da
    // regra do trigger: e so adicionar este script.
    private void Reset()
    {
        Rigidbody corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;

        // O collider do jogador vai de perto do chao ate 2 m, com 0,5 de
        // raio. Com a esfera a meio metro do chao e 0,6 de raio, ele pega
        // a pocao quando passa a menos de 1,1 m dela.
        SphereCollider area = GetComponent<SphereCollider>();
        area.isTrigger = true;
        area.center = new Vector3(0f, 0.5f, 0f);
        area.radius = 0.6f;
    }

    private void OnTriggerEnter(Collider other)
    {
        // InParent porque o collider costuma estar num filho e o resto no
        // objeto raiz - o mesmo motivo do clique de ataque.
        PotionBelt cinto = other.GetComponentInParent<PotionBelt>();

        // Cinto cheio: a pocao fica no chao, e o jogador pega quando
        // passar por ela de novo.
        if (cinto == null || !cinto.TryStore())
            return;

        Destroy(gameObject);
    }
}
