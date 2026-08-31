using UnityEngine;

public class DungeonSocket : MonoBehaviour
{
    [SerializeField] private string socketType = "Default";

    public string SocketType => socketType;

    public bool IsConnected { get; private set; }

    public DungeonModule Owner { get; private set; }

    public void Initialize(DungeonModule owner)
    {
        Owner = owner;
        IsConnected = false;
    }

    public bool IsCompatibleWith(DungeonSocket other)
    {
        return other != null && socketType == other.socketType;
    }

    public void Connect(DungeonSocket other)
    {
        IsConnected = true;

        if (other != null)
            other.IsConnected = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(transform.position, 0.1f);

        // O eixo Z+ (azul) deve apontar PARA FORA da sala.
        Gizmos.DrawLine(
            transform.position,
            transform.position + transform.forward * 0.75f
        );
    }
}