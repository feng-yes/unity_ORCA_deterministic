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
            currentPosition = value;
            endPosition = value;
            // endPosition = new Vector2(15, 15);  // 测试聚集在同一目的地
            reachEnd = true;
        }
    }

    public const int simulatorPeriod = 4;  // 运行一次模拟的周期
    public const int faceAngleTurnPerFrame = 10;
    public const int speed = 15;


    private Vector2 _startPosition = new Vector2();
    private Vector2 endPosition;
    public Vector2 currentVelocity;
    public Vector2 currentPosition;
    public int currentFaceAngle;
    public bool reachEnd = false;  // 到达了终点？
    private int lastSimulatorFrame;

    public int LastSimulatorFrame
    {
        get;
        set;
    }

    public Vector2 GetTargetPosition()
    {
        return endPosition;
        // return GameMainManager2.Instance.mousePosition;
    }
    
    public void ReachEnd()
    {
        reachEnd = true;
    }
    
    void UpdateForwardDirection()
    {
        // 方法1：使用当前面向角度
        Vector3 forwardDirection = new Vector3(
            Mathf.Cos(currentFaceAngle * Mathf.Deg2Rad),
            0,
            Mathf.Sin(currentFaceAngle * Mathf.Deg2Rad)
        ).normalized;

        // 更新物体的前向方向
        transform.forward = forwardDirection;
    }

    // Update is called once per frame
    void Update()
    {
        if (sid >= 0)
        {
            transform.position = new Vector3(currentPosition.x(), transform.position.y, currentPosition.y());
            // Vector2 vel = currentVelocity;
            // if (Math.Abs(vel.x()) > 0.0001f && Math.Abs(vel.y()) > 0.0001f)
            //     transform.forward = new Vector3(vel.x(), 0, vel.y()).normalized;
            UpdateForwardDirection();
            
            if (Input.GetMouseButton(1))
            {
                // endPosition = GameMainManager2.Instance.mousePosition;
                endPosition = -_startPosition;
                reachEnd = false;

                // Debug.Log(sid + ", " + Simulator.Instance.isAgentInCollision(sid));
            }
        }
        
    }
}