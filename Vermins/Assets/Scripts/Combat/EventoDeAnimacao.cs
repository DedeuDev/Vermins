using UnityEngine;

/// <summary>
/// Repassa os eventos de animacao pra quem sabe o que fazer com eles.
///
/// Existe por uma limitacao chata do Mecanim: um Animation Event so
/// consegue chamar metodo de um componente que esteja NO MESMO
/// GameObject do Animator. O Animator mora no modelo, que e filho, e o
/// PlayerCombat mora na raiz - entao sem este intermediario nao tem como
/// o clipe avisar o combate.
///
/// Nao pus a logica aqui dentro de proposito. Este script e so um fio:
/// no dia em que o inimigo tambem lancar magia, ele ganha o mesmo
/// componente no modelo dele e nada aqui muda.
///
/// COMO LIGAR: no FBX da magia, aba Animation, com o clipe selecionado,
/// posiciona a agulha no frame em que a mao termina o gesto e clica no
/// botao de adicionar evento. No campo Function escreve exatamente
/// SoltarMagia (sem parenteses). Tem que fazer nos dois clipes de
/// ataque, senao um dos dois golpes sai sem bola.
/// </summary>
[DisallowMultipleComponent]
public class EventoDeAnimacao : MonoBehaviour
{
    private PlayerCombat combate;

    private void Awake()
    {
        combate = GetComponentInParent<PlayerCombat>();

        if (combate == null)
        {
            Debug.LogWarning(
                $"[{name}] Nao achei PlayerCombat acima de mim. O evento " +
                "SoltarMagia nao vai levar a lugar nenhum.", this);
        }
    }

    /// <summary>
    /// Chamado pelo Animation Event do clipe de magia, no frame em que a
    /// mao solta. E este metodo que faz a bola nascer - se o evento nao
    /// estiver no clipe, o personagem faz o gesto e nao sai nada.
    /// </summary>
    public void SoltarMagia()
    {
        if (combate != null)
            combate.SoltarProjetil();
    }
}
