using UnityEngine;
using System.Collections.Generic;

public class SimpleGateSpawner : MonoBehaviour
{
    public GameObject andGatePrefab;
    public GameObject orGatePrefab;
    public GameObject notGatePrefab;
    public List<Transform> spawnPoints;  

    void Start()
    {
        SpawnGates();
    }

    void SpawnGates()
    {
        
        List<Transform> availableSpawnPoints = new List<Transform>(spawnPoints);

        
        SpawnGateAtRandomPoint(andGatePrefab, availableSpawnPoints);

        
        SpawnGateAtRandomPoint(orGatePrefab, availableSpawnPoints);

        
        SpawnGateAtRandomPoint(notGatePrefab, availableSpawnPoints);

        Debug.Log("Spawned 1 AND, 1 OR, 1 NOT gate at random positions");
    }

    void SpawnGateAtRandomPoint(GameObject gatePrefab, List<Transform> availablePoints)
    {
        if (availablePoints.Count > 0 && gatePrefab != null)
        {
            
            int randomIndex = Random.Range(0, availablePoints.Count);
            Transform spawnPoint = availablePoints[randomIndex];

            
            Instantiate(gatePrefab, spawnPoint.position, Quaternion.identity);

            
            availablePoints.RemoveAt(randomIndex);
        }
    }
}