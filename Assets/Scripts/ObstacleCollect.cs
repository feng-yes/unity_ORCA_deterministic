using System.Collections.Generic;
using RVO;
using SoftFloat;
using UnityEngine;
using Vector2 = RVO.Vector2;

public class ObstacleCollect : MonoBehaviour
{
    void Awake()
    {
        BoxCollider[] boxColliders = GetComponentsInChildren<BoxCollider>();
        for (int i = 0; i < boxColliders.Length; i++)
        {
            float minX = boxColliders[i].transform.position.x -
                         boxColliders[i].size.x * boxColliders[i].transform.lossyScale.x * 0.5f;
            float minZ = boxColliders[i].transform.position.z -
                         boxColliders[i].size.z * boxColliders[i].transform.lossyScale.z * 0.5f;
            float maxX = boxColliders[i].transform.position.x +
                         boxColliders[i].size.x * boxColliders[i].transform.lossyScale.x * 0.5f;
            float maxZ = boxColliders[i].transform.position.z +
                         boxColliders[i].size.z * boxColliders[i].transform.lossyScale.z * 0.5f;

            IList<Vector2> obstacle = new List<Vector2>();
            obstacle.Add(new Vector2((sfloat)maxX, (sfloat)maxZ));
            obstacle.Add(new Vector2((sfloat)minX, (sfloat)maxZ));
            obstacle.Add(new Vector2((sfloat)minX, (sfloat)minZ));
            obstacle.Add(new Vector2((sfloat)maxX, (sfloat)minZ));
            Simulator.Instance.addObstacle(obstacle);
        }
    }
}