using UnityEngine;

/// <summary>
/// Arquivo do Ian. A lista de props de uma dungeon: o que o
/// DungeonDressing espalha e quantos por sala e por corredor. Um asset por
/// dungeon, em Placeholders/Blockout/Props/.
///
/// Asset em vez de lista no proprio componente porque as duas dungeons
/// usam o mesmo DungeonGenerator.prefab: assim a cena sobrescreve uma
/// linha so (qual asset), em vez de cada item da lista.
/// </summary>
[CreateAssetMenu(fileName = "Props", menuName = "Vermins/Props da Dungeon")]
public class DungeonPropSet : ScriptableObject
{
    public enum Placement
    {
        Wall,   // encostado na parede
        Corner, // no canto entre duas paredes
        Floor   // solto no chao onde se anda, como a poca
    }

    [System.Serializable]
    public class Entry
    {
        [Tooltip("Pivo no chao, no centro do que ocupa, com o +Z apontando " +
                 "pra fora da parede.")]
        [SerializeField] private GameObject prefab;

        [SerializeField] private Placement placement;

        [Tooltip("Largura ao longo da parede (x) e fundo a partir dela (y), " +
                 "em metros. No chao vale o maior dos dois, como diametro.")]
        [SerializeField] private Vector2 size = new Vector2(0.7f, 0.7f);

        [Tooltip("Quantos tento por sala, de x a y.")]
        [SerializeField] private Vector2Int perRoom = new Vector2Int(0, 1);

        [Tooltip("Quantos tento por corredor, de x a y.")]
        [SerializeField] private Vector2Int perCorridor = Vector2Int.zero;

        public GameObject Prefab => prefab;
        public Placement Placement => placement;
        public Vector2 Size => size;
        public Vector2Int PerRoom => perRoom;
        public Vector2Int PerCorridor => perCorridor;
    }

    [SerializeField] private Entry[] entries;

    public Entry[] Entries => entries;
}
