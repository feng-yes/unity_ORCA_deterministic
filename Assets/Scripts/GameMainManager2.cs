using System;
using System.Collections.Generic;
using Lean;
using RVO;
using UnityEngine;
using UnityEngine.Assertions;
using Vector2 = RVO.Vector2;

public class GameMainManager2 : SingletonBehaviour<GameMainManager2>
{
    public GameObject agentPrefab;

    private const float logicframe = 0.05f;
    private const float AngleThreshold = 1f;  // 小于多少度算转身完成
    private const float stopDistanceThreshold = 0.5f;  // 小于多少距离算到达终点
    private const float stopVelocityThreshold = 0.01f;  // 速度小于多少度算调整完成，不再移动

    [HideInInspector] public Vector2 mousePosition;

    private Plane m_hPlane = new Plane(Vector3.up, Vector3.zero);
    
    private Dictionary<int, GameAgent2> m_agentMap = new Dictionary<int, GameAgent2>();
    private float timeSinceLastStep = 0f;

    private bool startLogicFrame = false;
    private float totalframe = 0f;

    // Use this for initialization
    void Start()
    {
        Simulator.Instance.setTimeStep(logicframe);
        Simulator.Instance.setAgentDefaults(15.0f, 10, 0.2f, 0.2f, 2.0f, GameAgent2.speed, new Vector2(0.0f, 0.0f));

        // 边界在 BoundaryPoint 加了 
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
            if (agent.reachEnd)
            {
                continue;
            }
            
            // 移动
            Vector2 aimPosition = agent.GetTargetPosition();
            Vector2 goalVector = aimPosition - agent.currentPosition;
            if (RVOMath.absSq(goalVector) > stopDistanceThreshold)
            {
                goalVector = RVOMath.normalize(goalVector) * GameAgent2.speed;
            }
            else
            {
                goalVector = new Vector2(0, 0);
            }
            Simulator.Instance.setAgentPrefVelocity(agent.sid, goalVector);
            
            Simulator.Instance.computeAgentNeighbors(agent.sid);
            Simulator.Instance.computeAgentNewVelocity(agent.sid);
            
            // 处理转身及速度
            Vector2 newVelocity = Simulator.Instance.getAgentNewVelocity(agent.sid);
            agent.currentVelocity = newVelocity;
            ProcessMovementAndRotation(agent);

            // 替代原有的 agent update
            Simulator.Instance.setAgentVelocity(agent.sid, agent.currentVelocity);
            Simulator.Instance.setAgentPosition(agent.sid, agent.currentPosition);
            
        }

        // 可以不设
        Simulator.Instance.setGlobalTime(totalframe);
    }
    
    void ProcessMovementAndRotation(GameAgent2 agent)
    {
        float targetAngle = agent.currentFaceAngle;
        float angleDifference = 0;
        Vector2 vel = agent.currentVelocity;
        if (Math.Abs(vel.x()) > stopVelocityThreshold || Math.Abs(vel.y()) > stopVelocityThreshold)
        {
            targetAngle = Mathf.Atan2(vel.y(), vel.x()) * Mathf.Rad2Deg;
            angleDifference = Mathf.DeltaAngle(agent.currentFaceAngle, targetAngle);
        }
        else
        {
            agent.ReachEnd();
        }

        float timeRemainingRatio = 1f;

        // 如果需要转向
        if (Mathf.Abs(angleDifference) > AngleThreshold)
        {
            // 计算这一帧需要的转向量
            float turnAmount = Mathf.Sign(angleDifference) * Mathf.Min(Mathf.Abs(angleDifference), GameAgent2.faceAngleTurnPerFrame);
            
            // 计算实际转向所用的时间比例
            timeRemainingRatio = 1f - (Mathf.Abs(turnAmount) / GameAgent2.faceAngleTurnPerFrame);

            // 更新当前朝向
            agent.currentFaceAngle = (int)Mathf.Repeat(agent.currentFaceAngle + turnAmount, 360f);

            // 如果转向未完成，则不进行移动
            if (Mathf.Abs(Mathf.DeltaAngle(agent.currentFaceAngle, targetAngle)) > AngleThreshold)
            {
                timeRemainingRatio = 0f;
            }
        }

        // 根据剩余时间比例进行移动
        if (timeRemainingRatio > 0)
        {
            agent.currentPosition += agent.currentVelocity * timeRemainingRatio * logicframe;
        }
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

        if (Input.GetMouseButtonUp(1))
        {
            startLogicFrame = true;
        }

        if (!startLogicFrame)
        {
            return;
        }

        // Simulator.Instance.doStep();
        // Accumulate the time that has passed since the last frame
        timeSinceLastStep += Time.deltaTime;
    
        // Check if enough time has passed to execute a simulation step
        if (timeSinceLastStep >= logicframe)
        {
            // Simulator.Instance.doStep();
            UpdateLogicFrame();
        
            // Reduce the accumulated time by the time step to accommodate for potentially larger time increments
            timeSinceLastStep -= logicframe;
        }
    }
}