using UnityEngine;
using UnityEngine.AI;

public class PlayerRaceController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Transform finishLine;
    
    private NavMeshAgent agent;
    private bool isRacing = false;
    
    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
    }
    
    public void StartRacing()
    {
        isRacing = true;
        agent.SetDestination(finishLine.position);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isRacing && other.transform == finishLine)
        {
            FinishRace();
        }
    }
    
    private void FinishRace()
    {
        isRacing = false;
        RaceManager.Instance.FinishRace(true);
    }
} 