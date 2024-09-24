using System;
using System.Collections;
using System.Collections.Generic;
using RVO;
using UnityEngine;
using Random = System.Random;
using Vector2 = RVO.Vector2;

public class GameAgent2 : MonoBehaviour
{
    [HideInInspector] public int sid = -1;

    public Vector2 StartPosition
    {
        get => _startPosition;
        set
        {
            _startPosition = value;
            endPosition = new Vector2(-_startPosition.x(), -_startPosition.y());
        }
    }

    private Vector2 _startPosition = new Vector2();
    private Vector2 endPosition;

    public Vector2 currentPosition;

    public int speed = 100;

    public Vector2 GetAimPosition()
    {
        return endPosition;
        // return GameMainManager2.Instance.mousePosition;
    }

    // Update is called once per frame
    void Update()
    {
        if (sid >= 0)
        {
            Vector2 pos = Simulator.Instance.getAgentPosition(sid);
            Vector2 vel = Simulator.Instance.getAgentVelocity(sid);
            transform.position = new Vector3(pos.x(), transform.position.y, pos.y());
            if (Math.Abs(vel.x()) > 0.0001f && Math.Abs(vel.y()) > 0.0001f)
                transform.forward = new Vector3(vel.x(), 0, vel.y()).normalized;
        }

        if (!Input.GetMouseButton(1))
        {
            Simulator.Instance.setAgentPrefVelocity(sid, new Vector2(0, 0));
            return;
        }
        
        // 测试 : 跟随鼠标 / 经过中间到达
        Vector2 goalVector = GameMainManager2.Instance.mousePosition - Simulator.Instance.getAgentPosition(sid);
        // Vector2 goalVector = endPosition - Simulator.Instance.getAgentPosition(sid);
        
        goalVector = RVOMath.normalize(goalVector) * speed;

        Simulator.Instance.setAgentPrefVelocity(sid, goalVector);

        /* Perturb a little to avoid deadlocks due to perfect symmetry. */
        // float angle = (float) m_random.NextDouble()*2.0f*(float) Math.PI;
        // float dist = (float) m_random.NextDouble()*0.0001f;
        //
        // Simulator.Instance.setAgentPrefVelocity(sid, Simulator.Instance.getAgentPrefVelocity(sid) +
        //                                              dist*
        //                                              new Vector2((float) Math.Cos(angle), (float) Math.Sin(angle)));
    }
}