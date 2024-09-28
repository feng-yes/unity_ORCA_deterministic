using System;
using System.Collections.Generic;
using Lean;
using RVO;
using SoftFloat;
using UnityEngine;
using UnityEngine.Assertions;
using Vector2 = RVO.Vector2;

public class GameMainManager : SingletonBehaviour<GameMainManager>
{
    public GameObject agentPrefab;

    [HideInInspector] public Vector2 mousePosition;

    private Plane m_hPlane = new Plane(Vector3.up, Vector3.zero);
    private Dictionary<int, GameAgent> m_agentMap = new Dictionary<int, GameAgent>();

    // Use this for initialization
    void Start()
    {
        Debug.Log("GameMainManager start");
        Simulator.Instance.setTimeStep((sfloat)0.25f);
        Simulator.Instance.setAgentDefaults((sfloat)15.0f, 10, (sfloat)5.0f, (sfloat)5.0f, (sfloat)2.0f, (sfloat)2.0f, new Vector2(sfloat.Zero, sfloat.Zero));

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
            GameAgent ga = go.GetComponent<GameAgent>();
            Assert.IsNotNull(ga);
            ga.sid = sid;
            ga.StartPosition = new Vector2(mousePosition.x(), mousePosition.y());
            m_agentMap.Add(sid, ga);
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

        Simulator.Instance.doStep();
    }
}