﻿using System;
using System.Collections;
using System.Collections.Generic;
using RVO;
using SoftFloat;
using UnityEngine;
using Random = System.Random;
using Vector2 = RVO.Vector2;

public class GameAgent : MonoBehaviour
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



    /** Random number generator. */
    private Random m_random = new Random();

    [SerializeField] private Vector2 _startPosition = new Vector2();
    private Vector2 endPosition;
    // Use this for initialization

    // Update is called once per frame
    void Update()
    {
        if (sid >= 0)
        {
            Vector2 pos = Simulator.Instance.getAgentPosition(sid);
            Vector2 vel = Simulator.Instance.getAgentVelocity(sid);
            transform.position = new Vector3((float)pos.x(), transform.position.y, (float)pos.y());
            if (Math.Abs((float)vel.x()) > 0.0001f && Math.Abs((float)vel.y()) > 0.0001f)
                transform.forward = new Vector3((float)vel.x(), 0, (float)vel.y()).normalized;
        }

        if (!Input.GetMouseButton(1))
        {
            Simulator.Instance.setAgentPrefVelocity(sid, new Vector2(sfloat.Zero, sfloat.Zero));
            return;
        }

        // 跟随鼠标
        Vector2 goalVector = GameMainManager.Instance.mousePosition - Simulator.Instance.getAgentPosition(sid);
        
        // 测试经过中间到达
        // Vector2 goalVector = endPosition - Simulator.Instance.getAgentPosition(sid);
        
        if (RVOMath.absSq(goalVector) > sfloat.One)
        {
            goalVector = RVOMath.normalize(goalVector);
        }

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