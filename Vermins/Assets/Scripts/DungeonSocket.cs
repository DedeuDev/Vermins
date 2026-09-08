using UnityEngine;

public class DungeonSocket : MonoBehaviour
{
    [SerializeField] private string socketType = "Default";

    public string SocketType => socketType;

    /*
     * Connected = conectado a outro módulo.
     *
     * Sealed = fechado por SocketBlocker.
     *
     * Agora são estados diferentes.
     */
    public bool IsConnected { get; private set; }

    public bool IsSealed { get; private set; }

    public DungeonModule Owner { get; private set; }

    /*
     * Socket localizado no outro módulo
     * desta conexão.
     */
    public DungeonSocket ConnectedSocket { get; private set; }

    /*
     * Um socket está resolvido quando:
     *
     * - está conectado;
     * OU
     * - foi fechado por parede.
     */
    public bool IsResolved =>
        IsConnected || IsSealed;

    public void Initialize(
        DungeonModule owner
    )
    {
        Owner = owner;

        IsConnected = false;

        IsSealed = false;

        ConnectedSocket = null;
    }

    // ==================================================
    // COMPATIBILIDADE
    // ==================================================

    public bool IsCompatibleWith(
        DungeonSocket other
    )
    {
        return
            other != null &&
            socketType == other.socketType;
    }

    // ==================================================
    // CONEXÃO
    // ==================================================

    public void Connect(
        DungeonSocket other
    )
    {
        if (other == null)
            return;

        /*
         * Este socket.
         */
        IsConnected = true;
        IsSealed = false;
        ConnectedSocket = other;

        /*
         * Socket do outro módulo.
         */
        other.IsConnected = true;
        other.IsSealed = false;
        other.ConnectedSocket = this;
    }

    // ==================================================
    // FECHAMENTO
    // ==================================================

    public void Seal()
    {
        /*
         * Um socket conectado a outro módulo
         * não pode ser transformado em parede.
         */
        if (IsConnected)
            return;

        IsSealed = true;

        ConnectedSocket = null;
    }

    // ==================================================
    // GIZMOS
    // ==================================================

    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(
            transform.position,
            0.1f
        );

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            transform.forward * 0.75f
        );
    }
}