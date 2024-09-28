using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RVO;
using SoftFloat;
using UnityEngine;
using Vector2 = RVO.Vector2;

public class BoundaryPoint : MonoBehaviour
{
    void Awake()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>();
        transforms = transforms.Where(c => c.gameObject != this.gameObject).ToArray();
        
        IList<Vector2> obstacle = new List<Vector2>();
        for (int i = 0; i < transforms.Length; i++)
        {
            obstacle.Add(new Vector2((sfloat)transforms[i].position.x, (sfloat)transforms[i].position.z));
        }

        Simulator.Instance.setBoundary(obstacle);
    }

}
