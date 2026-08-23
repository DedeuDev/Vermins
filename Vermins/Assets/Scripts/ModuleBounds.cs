using UnityEngine;

public class ModuleBounds : MonoBehaviour
{
    public BoxCollider[] boxColliders;

    private void Awake()
    {
        boxColliders = GetComponentsInChildren<BoxCollider>();
    }
}