using UnityEngine;

/// <summary>
/// Troca o cursor do mouse conforme o que esta embaixo dele: espada em
/// inimigo, balao em NPC, mao em item no chao, seta normal no resto.
///
/// Quem decide inimigo e NPC e o PlayerController.OQueEstaNoCursor, o
/// mesmo teste que o clique usa, entao o cursor nunca promete uma coisa
/// e o clique faz outra. O item eu olho aqui, porque o clique nao tem
/// ordem de pegar: clicar no item e andar ate ele, e quem pega e o
/// trigger.
///
/// So chamo o Cursor.SetCursor quando o tipo muda, nao todo frame.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CursorContextual : MonoBehaviour
{
    private enum Tipo { Normal, Ataque, Conversa, Pegar }

    [SerializeField] private Texture2D cursorDeAtaque;
    [SerializeField] private Vector2 pontaDoAtaque = new Vector2(3f, 3f);

    [SerializeField] private Texture2D cursorDeConversa;
    [SerializeField] private Vector2 pontaDaConversa = new Vector2(3f, 3f);

    [SerializeField] private Texture2D cursorDePegar;
    [SerializeField] private Vector2 pontaDoPegar = new Vector2(12f, 4f);

    [SerializeField] private float alcanceDoRaio = 200f;

    private PlayerController controller;
    private Health health;
    private Tipo atual = Tipo.Normal;

    // O raio do item atravessa trigger (a pocao e trigger), e na dungeon
    // tem trigger grande cobrindo a sala. Entao pego varios acertos e
    // olho em ordem de distancia, em vez de so o primeiro.
    private readonly RaycastHit[] acertos = new RaycastHit[16];

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        health = GetComponent<Health>();
    }

    private void OnDisable()
    {
        // Sem isso, trocar de cena ou morrer com o mouse num inimigo
        // deixava a espada presa no menu.
        Aplicar(Tipo.Normal);
    }

    private void Update()
    {
        Aplicar(Classificar());
    }

    private Tipo Classificar()
    {
        // Mesmos cortes do Update do PlayerController: morto ou pausado
        // o clique nao faz nada, entao o cursor tambem nao promete nada.
        if (health != null && health.IsDead)
            return Tipo.Normal;

        if (Time.timeScale == 0f)
            return Tipo.Normal;

        switch (controller.OQueEstaNoCursor(out Ray ray))
        {
            case PlayerController.AlvoDoCursor.Interagivel:
                return Tipo.Conversa;

            case PlayerController.AlvoDoCursor.Inimigo:
                return Tipo.Ataque;

            case PlayerController.AlvoDoCursor.Chao:
                return TemItem(ray) ? Tipo.Pegar : Tipo.Normal;

            default:
                return Tipo.Normal;
        }
    }

    private bool TemItem(Ray ray)
    {
        int quantos = Physics.RaycastNonAlloc(
            ray,
            acertos,
            alcanceDoRaio,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        System.Array.Sort(acertos, 0, quantos, PorDistancia);

        for (int i = 0; i < quantos; i++)
        {
            Collider c = acertos[i].collider;

            if (EhColetavel(c))
                return true;

            // Chao, parede ou inimigo na frente: o item atras dele nao
            // conta.
            if (!c.isTrigger)
                return false;
        }

        return false;
    }

    private static bool EhColetavel(Collider c)
    {
        return c.GetComponentInParent<PotionPickup>() != null ||
               c.GetComponentInParent<GoldPickup>() != null ||
               c.GetComponentInParent<JewelPickup>() != null;
    }

    private static readonly System.Collections.Generic.IComparer<RaycastHit> PorDistancia =
        System.Collections.Generic.Comparer<RaycastHit>.Create(
            (a, b) => a.distance.CompareTo(b.distance));

    private void Aplicar(Tipo tipo)
    {
        if (tipo == atual)
            return;

        atual = tipo;

        switch (tipo)
        {
            case Tipo.Ataque:
                Cursor.SetCursor(cursorDeAtaque, pontaDoAtaque, CursorMode.Auto);
                break;
            case Tipo.Conversa:
                Cursor.SetCursor(cursorDeConversa, pontaDaConversa, CursorMode.Auto);
                break;
            case Tipo.Pegar:
                Cursor.SetCursor(cursorDePegar, pontaDoPegar, CursorMode.Auto);
                break;
            default:
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                break;
        }
    }
}
