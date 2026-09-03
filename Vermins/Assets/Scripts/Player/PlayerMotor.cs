using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Cuida so de mover o jogador pelo NavMesh. Nao le input e nao
/// decide pra onde ir - quem manda e o PlayerController.
/// Separei assim porque depois o ataque, a IA e o sistema de quest
/// tambem vao precisar mandar o jogador pra algum lugar, e nenhum
/// deles deveria depender do mouse.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movimento")]
    [Tooltip("Casado com a velocidade do clipe de SprintForward, que e " +
             "o limiar de cima do blend tree. Mudar isso sem mexer la " +
             "faz o pe deslizar no talo, que e onde o personagem mais " +
             "fica. Se alguem trocar o clipe de sprint, rode o menu do " +
             "Mixamo e leia a velocidade nova do clipe.")]
    [SerializeField] private float moveSpeed = 4.13f;

    [Tooltip("Alto de proposito. O caso que decide isto nao e a " +
             "arrancada, e a inversao no meio da corrida: clicar pra " +
             "tras enquanto ele corre. Medi com 20 e ele passava 0,43 m " +
             "alem do ponto antes de conseguir voltar, e levava 0,40 s " +
             "pra estar correndo pro outro lado - le-se como freio de " +
             "carro. Com 80 sao 0,11 m e 0,10 s. " +
             "Nao custa nada na animacao: medi o escorregamento somado " +
             "num trajeto reto com 20, 40 e 80, e deu 0,25 m nos tres. " +
             "So a arrancada melhora, de 0,16 pra 0,08 m.")]
    [SerializeField] private float acceleration = 80f;

    [Tooltip("Teto de graus por segundo pra virar o corpo. Quem gira e " +
             "este script, nao o NavMeshAgent - veja o Girar() la embaixo. " +
             "Numa curva normal o giro nem encosta aqui; quem manda e o " +
             "tempoDeGiro. Este teto so pega na inversao de 180, que e o " +
             "giro mais violento que existe: sem ele o corpo dava saltos " +
             "de 21,7 graus de um frame pro outro a 60 fps, e com 900 cai " +
             "pra 13,9. Custa 0,07 s a mais pra completar a inversao.")]
    [SerializeField] private float angularSpeed = 900f;

    [Tooltip("Quanto tempo o corpo leva pra assentar na direcao nova. " +
             "Antes eu girava a taxa constante, e a 60 fps uma curva de " +
             "90 graus acabava em 3 frames, com o corpo pulando 25 graus " +
             "por frame e parando seco no fim - isso le-se como teleporte, " +
             "nao como virada. Com 0,08 a mesma curva leva 11 frames, o " +
             "maior salto cai pra 12,7 graus e ela desacelera no fim. " +
             "O preco e o corpo ficar mais torto DURANTE a virada: o erro " +
             "medio entre pra onde ele olha e pra onde ele anda, pesado " +
             "pela distancia andada, sobe de 1,3 pra 4,3 graus na curva de " +
             "90. Subir muito daqui pra cima comeca a parecer que ele anda " +
             "de lado - medi 0,18 e ja da 19,4% do tempo mais de 10 graus " +
             "torto.")]
    [SerializeField] private float tempoDeGiro = 0.08f;

    [Header("NavMesh")]
    [Tooltip("Altura do pivo do personagem acima dos pes dele. " +
             "Nao e 1 de proposito. O NavMesh bakeado nao fica colado no " +
             "chao: ele nasce meio voxel acima, e o voxel padrao e o raio " +
             "do agente dividido por 3. Com raio 0,5 da 0,083 m. Se eu " +
             "deixasse 1 aqui, o personagem andaria flutuando 8 cm - da " +
             "pra ver, ainda mais com sombra. Medi na cena: chao em 0, " +
             "NavMesh em 0,0833.")]
    [SerializeField] private float baseOffset = 0.9167f;

    [Tooltip("Distancia maxima entre o ponto pedido e o NavMesh pra " +
             "ele ainda valer como destino.")]
    [SerializeField] private float sampleDistance = 1f;

    private NavMeshAgent agent;

    // Embalo do giro. O SmoothDampAngle guarda a velocidade
    // angular aqui entre um frame e outro - e ela que faz o
    // corpo desacelerar em vez de parar seco.
    private float velocidadeAngular;

    /// <summary>Verdadeiro enquanto o jogador esta indo pra algum lugar.</summary>
    public bool IsMoving =>
        agent.hasPath && agent.remainingDistance > agent.stoppingDistance;

    public Vector3 Destination => agent.destination;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.baseOffset = baseOffset;

        // Copio o mesmo numero pro agente so pra o Inspector nao mentir.
        // Com updateRotation desligado ele nao usa esse campo pra nada,
        // mas quem abrisse veria 120 ali e acharia que e essa a
        // velocidade de giro.
        agent.angularSpeed = angularSpeed;

        // Tiro a rotacao do agente. Ele vira o corpo na direcao da
        // velocidade que ele JA tem, e essa chega atrasada: no meio de
        // uma virada o personagem corre de banda por um instante. Medi
        // num zigue-zague: com o agente girando, o corpo passava 14,6%
        // do tempo mais de 10 graus torto em relacao a pra onde andava.
        // Girando eu mesmo pela desiredVelocity, cai pra 1%.
        agent.updateRotation = false;
    }

    private void Update()
    {
        Girar();
    }

    /// <summary>
    /// Vira o corpo pra onde o agente QUER ir, nao pra onde ele ja
    /// conseguiu ir. A desiredVelocity muda no mesmo frame do clique,
    /// entao o personagem comeca a virar antes de sair do lugar - que e
    /// como um ARPG se comporta.
    /// O giro e amortecido, nao a taxa constante: ele arranca e assenta
    /// devagar. Ver o tooltip do tempoDeGiro pra o porque.
    /// </summary>
    private void Girar()
    {
        Vector3 direcao = agent.desiredVelocity;
        direcao.y = 0f;

        // Parado a desiredVelocity e lixo numerico. Sem esse corte o
        // personagem fica tremendo, e pior: brigaria com o PlayerCombat,
        // que e quem vira o corpo pro alvo na hora de bater.
        if (direcao.sqrMagnitude < 0.01f)
        {
            // Zerar o embalo aqui e obrigatorio. Se eu deixar a
            // velocidade angular guardada, ele para de andar ainda
            // girando e sai rodando sozinho no lugar.
            velocidadeAngular = 0f;
            return;
        }

        float alvo = Mathf.Atan2(direcao.x, direcao.z) * Mathf.Rad2Deg;

        float y = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            alvo,
            ref velocidadeAngular,
            tempoDeGiro,
            angularSpeed,
            Time.deltaTime
        );

        // Euler direto em vez de LookRotation porque assim eu tranco X e
        // Z em zero de graca: o corpo nunca inclina, nem em rampa.
        transform.rotation = Quaternion.Euler(0f, y, 0f);
    }

    /// <summary>
    /// Manda o jogador andar ate um ponto do mundo.
    /// Devolve false quando nao tem NavMesh perto o suficiente,
    /// que e o caso de clique no vazio ou fora do cenario.
    /// </summary>
    public bool MoveTo(Vector3 worldPoint)
    {
        if (!NavMesh.SamplePosition(
                worldPoint,
                out NavMeshHit hit,
                sampleDistance,
                NavMesh.AllAreas))
        {
            return false;
        }

        agent.isStopped = false;
        agent.SetDestination(hit.position);

        return true;
    }

    /// <summary>Para na hora e esquece o caminho atual.</summary>
    public void Stop()
    {
        if (!agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    private void OnDrawGizmosSelected()
    {
        if (agent == null || !agent.hasPath)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(agent.destination, 0.2f);

        Vector3[] corners = agent.path.corners;

        for (int i = 0; i < corners.Length - 1; i++)
        {
            Gizmos.DrawLine(corners[i], corners[i + 1]);
        }
    }
}
