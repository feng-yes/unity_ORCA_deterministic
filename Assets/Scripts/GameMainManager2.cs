﻿using System.Collections.Generic;
using Lean;
using RVO;
using UnityEngine;
using UnityEngine.Assertions;
using Vector2 = RVO.Vector2;

public class GameMainManager2 : SingletonBehaviour<GameMainManager2>
{
    public GameObject agentPrefab;

    [HideInInspector] public Vector2 mousePosition;

    private Plane m_hPlane = new Plane(Vector3.up, Vector3.zero);
    private Dictionary<int, GameAgent2> m_agentMap = new Dictionary<int, GameAgent2>();
    private float timeSinceLastStep = 0f;

    private float logicframe = 0.05f;
    private float totalframe = 0f;

    // Use this for initialization
    void Start()
    {
        Simulator.Instance.setTimeStep(0.02f);
        Simulator.Instance.setAgentDefaults(15.0f, 10, 0.05f, 0.05f, 2.0f, 50.0f, new Vector2(0.0f, 0.0f));

        // add in awake
        Simulator.Instance.processObstacles();
    }

    private void UpdateMousePosition()
    {
        Vector3 position = Vector3.zero;
        Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        float rayDistance;
        if (m_hPlane.Raycast(mouseRay, out rayDistance))
            position = mouseRay.GetPoint(rayDistance);

        mousePosition.x_ = position.x;
        mousePosition.y_ = position.z;
    }

    void DeleteAgent()
    {
        float rangeSq = float.MaxValue;
        int agentNo = Simulator.Instance.queryNearAgent(mousePosition, 1.5f);
        if (agentNo == -1 || !m_agentMap.ContainsKey(agentNo))
            return;

        Simulator.Instance.delAgent(agentNo);
        LeanPool.Despawn(m_agentMap[agentNo].gameObject);
        m_agentMap.Remove(agentNo);
    }

    void CreatAgent()
    {
        int sid = Simulator.Instance.addAgent(mousePosition);
        if (sid >= 0)
        {
            GameObject go = LeanPool.Spawn(agentPrefab, new Vector3(mousePosition.x(), 0, mousePosition.y()), Quaternion.identity);
            GameAgent2 ga = go.GetComponent<GameAgent2>();
            Assert.IsNotNull(ga);
            ga.sid = sid;
            ga.StartPosition = new Vector2(mousePosition.x(), mousePosition.y());
            m_agentMap.Add(sid, ga);
        }
    }

    private void UpdateLogicFrame()
    {
        totalframe = totalframe + logicframe;
        Simulator.Instance.doStepBefore();

        foreach (var vAgent in m_agentMap)
        {
            GameAgent2 agent = vAgent.Value;
            // 移动
            Vector2 aimPosition = agent.GetAimPosition();
            Vector2 goalVector = aimPosition - agent.currentPosition;
            goalVector = RVOMath.normalize(goalVector) * agent.speed;
            Simulator.Instance.setAgentPrefVelocity(agent.sid, goalVector);
            Simulator.Instance.computeAgentNeighbors(agent.sid);
            Simulator.Instance.computeAgentNewVelocity(agent.sid);
            
            // 处理转身及速度

            // 替代原有的 agent update
            Simulator.Instance.setAgentVelocity(agent.sid, new Vector2());
            Simulator.Instance.setAgentPosition(agent.sid, new Vector2());
        }

        // 可以不设
        Simulator.Instance.setGlobalTime(totalframe);
    }

    // Update is called once per frame
    private void Update()
    {
        UpdateMousePosition();
        if (Input.GetMouseButtonUp(0))
        {
            if (Input.GetKey(KeyCode.Delete))
            {
                DeleteAgent();
            }
            else
            {
                CreatAgent();
            }
        }

        // Simulator.Instance.doStep();
        // Accumulate the time that has passed since the last frame
        timeSinceLastStep += Time.deltaTime;
    
        // Check if enough time has passed to execute a simulation step
        if (timeSinceLastStep >= Simulator.Instance.timeStep_)
        {
            Simulator.Instance.doStep();
        
            // Reduce the accumulated time by the time step to accommodate for potentially larger time increments
            timeSinceLastStep -= Simulator.Instance.timeStep_;
        }
    }
}