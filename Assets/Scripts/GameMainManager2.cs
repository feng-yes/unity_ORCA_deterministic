using System;
using System.Collections.Generic;
using Lean;
using RVO;
using SoftFloat;
using UnityEngine;
using UnityEngine.Assertions;
using Vector2 = RVO.Vector2;

public class GameMainManager2 : SingletonBehaviour<GameMainManager2>
{
    public GameObject agentPrefab;

    private static readonly sfloat logicframe = (sfloat)0.05f;
    private static readonly sfloat AngleThreshold = (sfloat)1f;  // 小于多少度算转身完成
    private static readonly sfloat stopDistanceThreshold = (sfloat)0.8f;  // 小于多少距离算到达终点
    private static readonly sfloat stopVelocityThreshold = (sfloat)0.01f;  // 速度小于多少度算调整完成，不再移动

    [HideInInspector] public Vector2 mousePosition;

    private Plane m_hPlane = new Plane(Vector3.up, Vector3.zero);
    
    private Dictionary<int, GameAgent2> m_agentMap = new Dictionary<int, GameAgent2>();
    private sfloat timeSinceLastStep = sfloat.Zero;

    private bool startLogicFrame = false;
    private sfloat totalTime = sfloat.Zero;
    private int totalFrame = 0;

    // Use this for initialization
    void Start()
    {
        Simulator.Instance.setTimeStep(logicframe);
        Simulator.Instance.setAgentDefaults((sfloat)15.0f, 10, (sfloat)0.2f, (sfloat)0.2f, (sfloat)2.0f, (sfloat)GameAgent2.speed, new Vector2(sfloat.Zero, sfloat.Zero));

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

        mousePosition.x_ = (sfloat)position.x;
        mousePosition.y_ = (sfloat)position.z;
    }

    void DeleteAgent()
    {
        float rangeSq = float.MaxValue;
        int agentNo = Simulator.Instance.queryNearAgent(mousePosition, (sfloat)1.5f);
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
            GameObject go = LeanPool.Spawn(agentPrefab, new Vector3((float)mousePosition.x(), 0, (float)mousePosition.y()), Quaternion.identity);
            GameAgent2 ga = go.GetComponent<GameAgent2>();
            Assert.IsNotNull(ga);
            ga.sid = sid;
            ga.StartPosition = new Vector2(mousePosition.x(), mousePosition.y());
            // ga.StartPosition = new Vector2();
            m_agentMap.Add(sid, ga);
        }
    }

    private void UpdateLogicFrame()
    {
        totalTime = totalTime + logicframe;
        totalFrame++;
        Simulator.Instance.doStepBefore();

        foreach (var vAgent in m_agentMap)
        {
            GameAgent2 agent = vAgent.Value;
            if (agent.reachEnd)
            {
                continue;
            }

            if (totalFrame - agent.LastSimulatorFrame < GameAgent2.simulatorPeriod)
            {
                continue;
            }

            agent.LastSimulatorFrame = totalFrame;
            
            Vector2 aimPosition = agent.GetTargetPosition();
            Vector2 goalVector = aimPosition - agent.currentPosition;
            if (RVOMath.absSq(goalVector) > stopDistanceThreshold)
            {
                goalVector = RVOMath.normalize(goalVector) * (sfloat)GameAgent2.speed;
            }
            else
            {
                goalVector = new Vector2(sfloat.Zero, sfloat.Zero);
            }
            Simulator.Instance.setAgentPrefVelocity(agent.sid, goalVector);
            
            Simulator.Instance.computeAgentNeighbors(agent.sid);
            Simulator.Instance.computeAgentNewVelocity(agent.sid);
            
        }

        foreach (var vAgent in m_agentMap)
        {
            GameAgent2 agent = vAgent.Value;
            if (agent.reachEnd)
            {
                continue;
            }
            
            // todo 这里应该缓存下上面的结果
            Vector2 aimPosition = agent.GetTargetPosition();
            Vector2 goalVector = aimPosition - agent.currentPosition;
            if (RVOMath.absSq(goalVector) <= stopDistanceThreshold)
            {
                agent.ReachEnd();
                continue;
            }
            
            // 处理转身及速度
            Vector2 newVelocity = Simulator.Instance.getAgentNewVelocity(agent.sid);
            ProcessMovementAndRotation(agent, newVelocity);
            
            // 替代原有的 agent update
            Simulator.Instance.setAgentVelocity(agent.sid, agent.currentVelocity);
            Simulator.Instance.setAgentPosition(agent.sid, agent.currentPosition);
        }

        // 可以不设
        Simulator.Instance.setGlobalTime(totalTime);
    }
    
    void ProcessMovementAndRotation(GameAgent2 agent, Vector2 newVelocity)
    {
        sfloat targetAngle = (sfloat)agent.currentFaceAngle;
        sfloat angleDifference = sfloat.Zero;
        Vector2 vel = newVelocity;
        if (sfloat.Abs(vel.x()) > stopVelocityThreshold || sfloat.Abs(vel.y()) > stopVelocityThreshold)
        {
            targetAngle = libm.atan2f(vel.y(), vel.x()) * (sfloat)Mathf.Rad2Deg;
            angleDifference = (sfloat)Mathf.DeltaAngle(agent.currentFaceAngle, (float)targetAngle);
        }
        else
        {
            // 找不到合适的速度 或 已经接近了终点
            agent.ReachEnd();
        }

        sfloat timeRemainingRatio = sfloat.One;

        // 如果需要转向
        if (sfloat.Abs(angleDifference) > AngleThreshold)
        {
            // 计算这一帧需要的转向量
            sfloat turnAmount = (sfloat)angleDifference.Sign() * sfloat.Min(sfloat.Abs(angleDifference), (sfloat)GameAgent2.faceAngleTurnPerFrame);
            
            // 计算实际转向所用的时间比例
            timeRemainingRatio = sfloat.One - (sfloat.Abs(turnAmount) / (sfloat)GameAgent2.faceAngleTurnPerFrame);

            // 更新当前朝向
            agent.currentFaceAngle = (int)Mathf.Repeat(agent.currentFaceAngle + (float)turnAmount, 360f);

            // 如果转向未完成，则不进行移动
            if (Mathf.Abs(Mathf.DeltaAngle(agent.currentFaceAngle, (float)targetAngle)) > (float)AngleThreshold)
            {
                timeRemainingRatio = sfloat.Zero;
            }
        }
        
        agent.currentVelocity = newVelocity;
        // agent.currentVelocity = newVelocity * timeRemainingRatio;

        // 根据剩余时间比例进行移动
        if (timeRemainingRatio > sfloat.Zero)
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
        timeSinceLastStep += (sfloat)Time.deltaTime;
    
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