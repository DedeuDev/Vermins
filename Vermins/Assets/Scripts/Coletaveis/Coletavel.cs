using UnityEngine;

/// <summary>
/// Base de tudo que fica no chao e o jogador pega encostando: ouro, joia,
/// pocao. Quem herda so diz o que entregar e pra quem, no Entregar.
///
/// Fiz depois do terceiro coletavel. Os tres repetiam a mesma montagem e
/// a mesma trava contra entrega dupla, e o CursorContextual tinha que
/// listar um por um. Coletavel novo herda daqui e ja ganha tudo isso.
///
/// O Rigidbody kinematic fica aqui, e nao no jogador. Trigger so dispara
/// se um dos dois lados tiver Rigidbody, e o NavMeshAgent do jogador nao
/// conta como um. Rigidbody no jogador faria a fisica brigar com o agent
/// pela posicao dele. E a mesma regra da flecha do boss.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public abstract class Coletavel : MonoBehaviour
{
    private bool coletado;

    /// <summary>
    /// Se tenta de novo enquanto quem nao pode levar continua em cima. A
    /// pocao liga: com o cinto cheio ela fica no chao, e se o jogador
    /// beber sem sair de cima ela tem que entrar. Ouro e joia nao
    /// precisam, porque quem tem carteira sempre leva na primeira.
    /// </summary>
    protected virtual bool TentarDeNovoEmCima => false;

    // O Reset roda quando o componente e adicionado no editor. Deixo o
    // Rigidbody e a esfera prontos aqui pra ninguem precisar lembrar da
    // regra do trigger: e so adicionar o script.
    protected virtual void Reset()
    {
        Rigidbody corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;

        // O collider do jogador vai de perto do chao ate 2 m, com 0,5 de
        // raio. Com a esfera a meio metro do chao e 0,6 de raio, ele pega
        // o item quando passa a menos de 1,1 m dele.
        SphereCollider area = GetComponent<SphereCollider>();
        area.isTrigger = true;
        area.center = new Vector3(0f, 0.5f, 0f);
        area.radius = 0.6f;
    }

    private void OnTriggerEnter(Collider other)
    {
        TentarColetar(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (TentarDeNovoEmCima)
            TentarColetar(other);
    }

    private void TentarColetar(Collider other)
    {
        // O trigger avisa uma vez por collider que entra, e o Destroy so
        // tira o item do jogo no fim do frame. Hoje o Player tem um
        // collider so e nao acontece nada. Com um segundo collider nele,
        // medi 20 itens entregando em dobro: 212 de ouro em vez de 106, e
        // 10 joias virando 20.
        if (coletado)
            return;

        // Marco antes de entregar, pra uma reentrada pelo OnChanged de
        // quem recebe tambem nao pegar o mesmo item de novo.
        coletado = true;

        if (!Entregar(other))
        {
            coletado = false;
            return;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Entrega o item pra quem encostou. Devolve false se esse collider
    /// nao leva (nao tem carteira, cinto cheio), e ai o item fica no
    /// chao. Procure com GetComponentInParent: o collider costuma estar
    /// num filho e o resto no objeto raiz.
    /// </summary>
    protected abstract bool Entregar(Collider quemEncostou);
}
