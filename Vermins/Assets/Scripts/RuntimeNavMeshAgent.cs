using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class RuntimeNavMeshAgent : MonoBehaviour
{
    private NavMeshAgent agent;

    private IEnumerator Start()
    {
        agent = GetComponent<NavMeshAgent>();

        NavMeshHit navMeshHit;

        while (!NavMesh.SamplePosition(
            transform.position,
            out navMeshHit,
            100f,
            NavMesh.AllAreas))
        {
            yield return null;
        }

        agent.enabled = true;

        agent.Warp(navMeshHit.position);
    }
}