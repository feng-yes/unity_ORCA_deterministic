/*
 * Agent.cs
 * RVO2 Library C#
 *
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Please send all bug reports to <geom@cs.unc.edu>.
 *
 * The authors may be contacted via:
 *
 * Jur van den Berg, Stephen J. Guy, Jamie Snape, Ming C. Lin, Dinesh Manocha
 * Dept. of Computer Science
 * 201 S. Columbia St.
 * Frederick P. Brooks, Jr. Computer Science Bldg.
 * Chapel Hill, N.C. 27599-3175
 * United States of America
 *
 * <http://gamma.cs.unc.edu/RVO2/>
 */

using System;
using System.Collections.Generic;
using SoftFloat;

namespace RVO
{
    /**
     * <summary>Defines an agent in the simulation.</summary>
     */
    internal class Agent
    {
        
        /**
         * 在 computeNeighbors 步骤构建
         * KeyValuePair 的 k 是跟 邻居Agent 的 距离平方
         */
        internal IList<KeyValuePair<sfloat, Agent>> agentNeighbors_ = new List<KeyValuePair<sfloat, Agent>>();
        internal IList<KeyValuePair<sfloat, Obstacle>> obstacleNeighbors_ = new List<KeyValuePair<sfloat, Obstacle>>();
        internal IList<Line> orcaLines_ = new List<Line>();
        internal Vector2 position_;
        
        // 期望速度，是算法的优化目标，即无碰撞约束下的目标运动方向
        internal Vector2 prefVelocity_;
        // 期望位置，可用于计算 prefVelocity_, 二者选其一
        internal Vector2 prefPosition_;
        
        // 实际速度，计算时会以这个作为 agent 的速度
        internal Vector2 velocity_;
        internal int id_ = 0;
        // 在导航中考虑的其他agent的最大数量
        internal int maxNeighbors_ = 0;
        internal sfloat maxSpeed_;
        // 在导航中考虑的与其他agent的最大距离（中心点到中心点），超过就不考虑避让
        internal sfloat neighborDist_;
        // 半径或社交距离，侵入这个距离内就算碰撞
        internal sfloat radius_;
        // 模拟计算出的此代理相对于其他agent的速度安全的最短时间。此数字越大，此代理对其他代理的出现做出反应的速度越快，但此代理在选择速度时拥有的自由度就越小。
        internal sfloat timeHorizon_;
        // 模拟计算出的此代理的速度相对于障碍物而言是安全的最短时间。
        internal sfloat timeHorizonObst_;
        internal bool needDelete_ = false;

        // computeNewVelocity 计算出来的速度
        private Vector2 newVelocity_;

        public Vector2 GetNewVelocity()
        {
            return newVelocity_;
        }

        // 是否碰撞了
        public bool IsInCollision()
        {
            // todo obstacle
            
            foreach (var edge in Simulator.Instance.boundaryEdges)
            {
                Vector2 relativePosition = position_ - edge.point;
                if (RVOMath.det(edge.direction, relativePosition) > RVOMath.COLLISION_THRESHOLD)
                {
                    return true;
                }
            }

            for (int i = 0; i < agentNeighbors_.Count; ++i)
            {
                Agent other = agentNeighbors_[i].Value;
                Vector2 relativePosition = other.position_ - position_;
                sfloat distSq = RVOMath.absSq(relativePosition);
                sfloat combinedRadius = radius_ + other.radius_;
                sfloat combinedRadiusSq = RVOMath.sqr(combinedRadius);
                if (distSq + RVOMath.COLLISION_THRESHOLD <= combinedRadiusSq)
                {
                    return true;
                }
            }

            return false;
        }
        
        /**
         * <summary>Computes the neighbors of this agent.</summary>
         */
        internal void computeNeighbors()
        {
            obstacleNeighbors_.Clear();
            sfloat rangeSq = RVOMath.sqr(timeHorizonObst_ * maxSpeed_ + radius_);
            Simulator.Instance.kdTree_.computeObstacleNeighbors(this, rangeSq);

            agentNeighbors_.Clear();

            if (maxNeighbors_ > 0)
            {
                rangeSq = RVOMath.sqr(neighborDist_);
                Simulator.Instance.kdTree_.computeAgentNeighbors(this, ref rangeSq);
            }
        }

        /**
         * <summary>Computes the new velocity of this agent.</summary>
         */
        internal void computeNewVelocity()
        {
            orcaLines_.Clear();

            sfloat invTimeHorizonObst = (sfloat)1 / timeHorizonObst_;

            /* Create obstacle ORCA lines. */
            for (int i = 0; i < obstacleNeighbors_.Count; ++i)
            {

                Obstacle obstacle1 = obstacleNeighbors_[i].Value;
                Obstacle obstacle2 = obstacle1.next_;

                Vector2 relativePosition1 = obstacle1.point_ - position_;
                Vector2 relativePosition2 = obstacle2.point_ - position_;

                /*
                 * Check if velocity obstacle of obstacle is already taken care
                 * of by previously constructed obstacle ORCA lines.
                 */
                bool alreadyCovered = false;

                for (int j = 0; j < orcaLines_.Count; ++j)
                {
                    if (RVOMath.det(invTimeHorizonObst * relativePosition1 - orcaLines_[j].point, orcaLines_[j].direction) - invTimeHorizonObst * radius_ >= -RVOMath.RVO_EPSILON && RVOMath.det(invTimeHorizonObst * relativePosition2 - orcaLines_[j].point, orcaLines_[j].direction) - invTimeHorizonObst * radius_ >= -RVOMath.RVO_EPSILON)
                    {
                        alreadyCovered = true;

                        break;
                    }
                }

                if (alreadyCovered)
                {
                    continue;
                }

                /* Not yet covered. Check for collisions. */
                sfloat distSq1 = RVOMath.absSq(relativePosition1);
                sfloat distSq2 = RVOMath.absSq(relativePosition2);

                sfloat radiusSq = RVOMath.sqr(radius_);

                Vector2 obstacleVector = obstacle2.point_ - obstacle1.point_;
                sfloat s = (-relativePosition1 * obstacleVector) / RVOMath.absSq(obstacleVector);
                sfloat distSqLine = RVOMath.absSq(-relativePosition1 - s * obstacleVector);

                Line line;

                if (s < sfloat.Zero && distSq1 <= radiusSq)
                {
                    /* Collision with left vertex. Ignore if non-convex. */
                    if (obstacle1.convex_)
                    {
                        line.point = new Vector2(sfloat.Zero, sfloat.Zero);
                        line.direction = RVOMath.normalize(new Vector2(-relativePosition1.y(), relativePosition1.x()));
                        orcaLines_.Add(line);
                    }

                    continue;
                }
                else if (s > (sfloat)1 && distSq2 <= radiusSq)
                {
                    /*
                     * Collision with right vertex. Ignore if non-convex or if
                     * it will be taken care of by neighboring obstacle.
                     */
                    if (obstacle2.convex_ && RVOMath.det(relativePosition2, obstacle2.direction_) >= sfloat.Zero)
                    {
                        line.point = new Vector2(sfloat.Zero, sfloat.Zero);
                        line.direction = RVOMath.normalize(new Vector2(-relativePosition2.y(), relativePosition2.x()));
                        orcaLines_.Add(line);
                    }

                    continue;
                }
                else if (s >= sfloat.Zero && s < (sfloat)1 && distSqLine <= radiusSq)
                {
                    /* Collision with obstacle segment. */
                    line.point = new Vector2(sfloat.Zero, sfloat.Zero);
                    line.direction = -obstacle1.direction_;
                    orcaLines_.Add(line);

                    continue;
                }

                /*
                 * No collision. Compute legs. When obliquely viewed, both legs
                 * can come from a single vertex. Legs extend cut-off line when
                 * non-convex vertex.
                 */

                Vector2 leftLegDirection, rightLegDirection;

                if (s < sfloat.Zero && distSqLine <= radiusSq)
                {
                    /*
                     * Obstacle viewed obliquely so that left vertex
                     * defines velocity obstacle.
                     */
                    if (!obstacle1.convex_)
                    {
                        /* Ignore obstacle. */
                        continue;
                    }

                    obstacle2 = obstacle1;

                    sfloat leg1 = RVOMath.sqrt(distSq1 - radiusSq);
                    leftLegDirection = new Vector2(relativePosition1.x() * leg1 - relativePosition1.y() * radius_, relativePosition1.x() * radius_ + relativePosition1.y() * leg1) / distSq1;
                    rightLegDirection = new Vector2(relativePosition1.x() * leg1 + relativePosition1.y() * radius_, -relativePosition1.x() * radius_ + relativePosition1.y() * leg1) / distSq1;
                }
                else if (s > (sfloat)1 && distSqLine <= radiusSq)
                {
                    /*
                     * Obstacle viewed obliquely so that
                     * right vertex defines velocity obstacle.
                     */
                    if (!obstacle2.convex_)
                    {
                        /* Ignore obstacle. */
                        continue;
                    }

                    obstacle1 = obstacle2;

                    sfloat leg2 = RVOMath.sqrt(distSq2 - radiusSq);
                    leftLegDirection = new Vector2(relativePosition2.x() * leg2 - relativePosition2.y() * radius_, relativePosition2.x() * radius_ + relativePosition2.y() * leg2) / distSq2;
                    rightLegDirection = new Vector2(relativePosition2.x() * leg2 + relativePosition2.y() * radius_, -relativePosition2.x() * radius_ + relativePosition2.y() * leg2) / distSq2;
                }
                else
                {
                    /* Usual situation. */
                    if (obstacle1.convex_)
                    {
                        sfloat leg1 = RVOMath.sqrt(distSq1 - radiusSq);
                        leftLegDirection = new Vector2(relativePosition1.x() * leg1 - relativePosition1.y() * radius_, relativePosition1.x() * radius_ + relativePosition1.y() * leg1) / distSq1;
                    }
                    else
                    {
                        /* Left vertex non-convex; left leg extends cut-off line. */
                        leftLegDirection = -obstacle1.direction_;
                    }

                    if (obstacle2.convex_)
                    {
                        sfloat leg2 = RVOMath.sqrt(distSq2 - radiusSq);
                        rightLegDirection = new Vector2(relativePosition2.x() * leg2 + relativePosition2.y() * radius_, -relativePosition2.x() * radius_ + relativePosition2.y() * leg2) / distSq2;
                    }
                    else
                    {
                        /* Right vertex non-convex; right leg extends cut-off line. */
                        rightLegDirection = obstacle1.direction_;
                    }
                }

                /*
                 * Legs can never point into neighboring edge when convex
                 * vertex, take cutoff-line of neighboring edge instead. If
                 * velocity projected on "foreign" leg, no constraint is added.
                 */

                Obstacle leftNeighbor = obstacle1.previous_;

                bool isLeftLegForeign = false;
                bool isRightLegForeign = false;

                if (obstacle1.convex_ && RVOMath.det(leftLegDirection, -leftNeighbor.direction_) >= sfloat.Zero)
                {
                    /* Left leg points into obstacle. */
                    leftLegDirection = -leftNeighbor.direction_;
                    isLeftLegForeign = true;
                }
                
                if (obstacle2.convex_ && RVOMath.det(rightLegDirection, obstacle2.direction_) <= sfloat.Zero)
                {
                    /* Right leg points into obstacle. */
                    rightLegDirection = obstacle2.direction_;
                    isRightLegForeign = true;
                }

                /* Compute cut-off centers. */
                Vector2 leftCutOff = invTimeHorizonObst * (obstacle1.point_ - position_);
                Vector2 rightCutOff = invTimeHorizonObst * (obstacle2.point_ - position_);
                Vector2 cutOffVector = rightCutOff - leftCutOff;

                /* Project current velocity on velocity obstacle. */

                /* Check if current velocity is projected on cutoff circles. */
                sfloat t = obstacle1 == obstacle2 ? (sfloat)0.5f : ((velocity_ - leftCutOff) * cutOffVector) / RVOMath.absSq(cutOffVector);
                sfloat tLeft = (velocity_ - leftCutOff) * leftLegDirection;
                sfloat tRight = (velocity_ - rightCutOff) * rightLegDirection;

                if ((t < sfloat.Zero && tLeft < sfloat.Zero) || (obstacle1 == obstacle2 && tLeft < sfloat.Zero && tRight < sfloat.Zero))
                {
                    /* Project on left cut-off circle. */
                    Vector2 unitW = RVOMath.normalize(velocity_ - leftCutOff);

                    line.direction = new Vector2(unitW.y(), -unitW.x());
                    line.point = leftCutOff + radius_ * invTimeHorizonObst * unitW;
                    orcaLines_.Add(line);

                    continue;
                }
                else if (t > sfloat.One && tRight < sfloat.Zero)
                {
                    /* Project on right cut-off circle. */
                    Vector2 unitW = RVOMath.normalize(velocity_ - rightCutOff);

                    line.direction = new Vector2(unitW.y(), -unitW.x());
                    line.point = rightCutOff + radius_ * invTimeHorizonObst * unitW;
                    orcaLines_.Add(line);

                    continue;
                }

                /*
                 * Project on left leg, right leg, or cut-off line, whichever is
                 * closest to velocity.
                 */
                sfloat distSqCutoff = (t < sfloat.Zero || t > sfloat.One || obstacle1 == obstacle2) ? sfloat.PositiveInfinity : RVOMath.absSq(velocity_ - (leftCutOff + t * cutOffVector));
                sfloat distSqLeft = tLeft < sfloat.Zero ? sfloat.PositiveInfinity : RVOMath.absSq(velocity_ - (leftCutOff + tLeft * leftLegDirection));
                sfloat distSqRight = tRight < sfloat.Zero ? sfloat.PositiveInfinity : RVOMath.absSq(velocity_ - (rightCutOff + tRight * rightLegDirection));

                if (distSqCutoff <= distSqLeft && distSqCutoff <= distSqRight)
                {
                    /* Project on cut-off line. */
                    line.direction = -obstacle1.direction_;
                    line.point = leftCutOff + radius_ * invTimeHorizonObst * new Vector2(-line.direction.y(), line.direction.x());
                    orcaLines_.Add(line);

                    continue;
                }

                if (distSqLeft <= distSqRight)
                {
                    /* Project on left leg. */
                    if (isLeftLegForeign)
                    {
                        continue;
                    }

                    line.direction = leftLegDirection;
                    line.point = leftCutOff + radius_ * invTimeHorizonObst * new Vector2(-line.direction.y(), line.direction.x());
                    orcaLines_.Add(line);

                    continue;
                }

                /* Project on right leg. */
                if (isRightLegForeign)
                {
                    continue;
                }

                line.direction = -rightLegDirection;
                line.point = rightCutOff + radius_ * invTimeHorizonObst * new Vector2(-line.direction.y(), line.direction.x());
                orcaLines_.Add(line);
            }
            
            // 将边界边转换为 ORCA 线
            foreach (var edge in Simulator.Instance.boundaryEdges)
            {
                Line line = new Line();
                Vector2 relativePosition = position_ - edge.point;
            
                // 如果 agent 已经在边界外，则强制其向内移动
                if (RVOMath.det(edge.direction, relativePosition) > sfloat.Zero)
                {
                    line.point = new Vector2(sfloat.Zero, sfloat.Zero);
                    line.direction = -edge.direction;
                }
                else
                {
                    // 否则，创建一个 ORCA 线来避免穿过边界
                    sfloat invTimeStep = sfloat.One / Simulator.Instance.timeStep_;
                    line.direction = -edge.direction;
                    line.point = invTimeStep * (edge.point + line.direction * radius_);
                    
                    // Vector2 u = velocity_ - (edge.point + edge.direction * (-relativePosition * edge.direction));
                    // line.point = velocity_ - u;
                    // line.direction = RVOMath.normalize(u);
                }
            
                orcaLines_.Add(line);
            }
            
            int numObstLines = orcaLines_.Count;

            sfloat invTimeHorizon = sfloat.One / timeHorizon_;

            /* Create agent ORCA lines. */
            for (int i = 0; i < agentNeighbors_.Count; ++i)
            {
                Agent other = agentNeighbors_[i].Value;

                Vector2 relativePosition = other.position_ - position_;
                Vector2 relativeVelocity = velocity_ - other.velocity_;
                sfloat distSq = RVOMath.absSq(relativePosition);
                sfloat combinedRadius = radius_ + other.radius_;
                sfloat combinedRadiusSq = RVOMath.sqr(combinedRadius);

                Line line;
                Vector2 u;

                if (distSq > combinedRadiusSq)
                {
                    // 没接触
                    /* No collision. */
                    Vector2 w = relativeVelocity - invTimeHorizon * relativePosition;

                    /* Vector from cutoff center to relative velocity. */
                    sfloat wLengthSq = RVOMath.absSq(w);
                    sfloat dotProduct1 = w * relativePosition;

                    if (dotProduct1 < sfloat.Zero && RVOMath.sqr(dotProduct1) > combinedRadiusSq * wLengthSq)
                    {
                        /* Project on cut-off circle. */
                        sfloat wLength = RVOMath.sqrt(wLengthSq);
                        Vector2 unitW = w / wLength;

                        line.direction = new Vector2(unitW.y(), -unitW.x());
                        u = (combinedRadius * invTimeHorizon - wLength) * unitW;
                    }
                    else
                    {
                        /* Project on legs. */
                        sfloat leg = RVOMath.sqrt(distSq - combinedRadiusSq);

                        if (RVOMath.det(relativePosition, w) > sfloat.Zero)
                        {
                            /* Project on left leg. */
                            line.direction = new Vector2(relativePosition.x() * leg - relativePosition.y() * combinedRadius, relativePosition.x() * combinedRadius + relativePosition.y() * leg) / distSq;
                        }
                        else
                        {
                            /* Project on right leg. */
                            line.direction = -new Vector2(relativePosition.x() * leg + relativePosition.y() * combinedRadius, -relativePosition.x() * combinedRadius + relativePosition.y() * leg) / distSq;
                        }

                        sfloat dotProduct2 = relativeVelocity * line.direction;
                        u = dotProduct2 * line.direction - relativeVelocity;
                    }
                }
                else
                {
                    /* Collision. Project on cut-off circle of time timeStep. */
                    sfloat invTimeStep = sfloat.One / Simulator.Instance.timeStep_;

                    /* Vector from cutoff center to relative velocity. */
                    Vector2 w = relativeVelocity - invTimeStep * relativePosition;

                    sfloat wLength = RVOMath.abs(w);
                    Vector2 unitW = w / wLength;

                    line.direction = new Vector2(unitW.y(), -unitW.x());
                    u = (combinedRadius * invTimeStep - wLength) * unitW;
                }

                line.point = velocity_ + (sfloat)0.5f * u;
                orcaLines_.Add(line);
            }

            int lineFail = linearProgram2(orcaLines_, maxSpeed_, prefVelocity_, false, ref newVelocity_);

            if (lineFail < orcaLines_.Count)
            {
                linearProgram3(orcaLines_, numObstLines, lineFail, maxSpeed_, ref newVelocity_);
            }
        }

        /**
         * <summary>Inserts an agent neighbor into the set of neighbors of this
         * agent.</summary>
         *
         * <param name="agent">A pointer to the agent to be inserted.</param>
         * <param name="rangeSq">The squared range around this agent.</param>
         */
        internal void insertAgentNeighbor(Agent agent, ref sfloat rangeSq)
        {
            if (this != agent)
            {
                sfloat distSq = RVOMath.absSq(position_ - agent.position_);

                if (distSq < rangeSq)
                {
                    if (agentNeighbors_.Count < maxNeighbors_)
                    {
                        agentNeighbors_.Add(new KeyValuePair<sfloat, Agent>(distSq, agent));
                    }

                    int i = agentNeighbors_.Count - 1;

                    // 新插入的邻居按照距离排序。
                    while (i != 0 && distSq < agentNeighbors_[i - 1].Key)
                    {
                        agentNeighbors_[i] = agentNeighbors_[i - 1];
                        --i;
                    }

                    agentNeighbors_[i] = new KeyValuePair<sfloat, Agent>(distSq, agent);

                    // 已达到最大大小，更新 rangeSq ，使得算法每次只保留最近的邻居。
                    if (agentNeighbors_.Count == maxNeighbors_)
                    {
                        rangeSq = agentNeighbors_[agentNeighbors_.Count - 1].Key;
                    }
                }
            }
        }

        /**
         * <summary>Inserts a static obstacle neighbor into the set of neighbors
         * of this agent.</summary>
         *
         * <param name="obstacle">The number of the static obstacle to be
         * inserted.</param>
         * <param name="rangeSq">The squared range around this agent.</param>
         */
        internal void insertObstacleNeighbor(Obstacle obstacle, sfloat rangeSq)
        {
            Obstacle nextObstacle = obstacle.next_;

            sfloat distSq = RVOMath.distSqPointLineSegment(obstacle.point_, nextObstacle.point_, position_);

            if (distSq < rangeSq)
            {
                obstacleNeighbors_.Add(new KeyValuePair<sfloat, Obstacle>(distSq, obstacle));

                int i = obstacleNeighbors_.Count - 1;

                while (i != 0 && distSq < obstacleNeighbors_[i - 1].Key)
                {
                    obstacleNeighbors_[i] = obstacleNeighbors_[i - 1];
                    --i;
                }
                obstacleNeighbors_[i] = new KeyValuePair<sfloat, Obstacle>(distSq, obstacle);
            }
        }

        /**
         * <summary>Updates the two-dimensional position and two-dimensional
         * velocity of this agent.</summary>
         */
        internal void update()
        {
            velocity_ = newVelocity_;
            position_ += velocity_ * Simulator.Instance.timeStep_;
        }

        /**
         * <summary>Solves a one-dimensional linear program on a specified line
         * subject to linear constraints defined by lines and a circular
         * constraint.</summary>
         *
         * 线性规划中求解沿着指定线段的最优速度，受限于线性约束（由多条直线定义）和圆形约束
         *
         * <returns>True if successful.</returns>
         *
         * <param name="lines">Lines defining the linear constraints.</param>
         * <param name="lineNo">The specified line constraint.</param>
         * <param name="radius">The radius of the circular constraint.</param>
         * <param name="optVelocity">The optimization velocity.</param>
         * <param name="directionOpt">True if the direction should be optimized.
         * </param>
         * <param name="result">A reference to the result of the linear program.
         * </param>
         */
        private bool linearProgram1(IList<Line> lines, int lineNo, sfloat radius, Vector2 optVelocity, bool directionOpt, ref Vector2 result)
        {
            sfloat dotProduct = lines[lineNo].point * lines[lineNo].direction;
            sfloat discriminant = RVOMath.sqr(dotProduct) + RVOMath.sqr(radius) - RVOMath.absSq(lines[lineNo].point);

            if (discriminant < sfloat.Zero)
            {
                // 当前线性约束与圆形约束没有交点
                /* Max speed circle fully invalidates line lineNo. */
                return false;
            }

            sfloat sqrtDiscriminant = RVOMath.sqrt(discriminant);
            // 线性约束的最左和最右的两个交点
            sfloat tLeft = -dotProduct - sqrtDiscriminant;
            sfloat tRight = -dotProduct + sqrtDiscriminant;

            for (int i = 0; i < lineNo; ++i)
            {
                sfloat denominator = RVOMath.det(lines[lineNo].direction, lines[i].direction);
                sfloat numerator = RVOMath.det(lines[i].direction, lines[lineNo].point - lines[i].point);

                if (RVOMath.fabs(denominator) <= RVOMath.RVO_EPSILON)
                {
                    /* Lines lineNo and i are (almost) parallel. */
                    if (numerator < sfloat.Zero)
                    {
                        return false;
                    }

                    continue;
                }

                sfloat t = numerator / denominator;

                if (denominator >= sfloat.Zero)
                {
                    /* Line i bounds line lineNo on the right. */
                    tRight = sfloat.Min(tRight, t);
                }
                else
                {
                    /* Line i bounds line lineNo on the left. */
                    tLeft = sfloat.Max(tLeft, t);
                }

                if (tLeft > tRight)
                {
                    return false;
                }
            }

            if (directionOpt)
            {
                /* Optimize direction. */
                if (optVelocity * lines[lineNo].direction > sfloat.Zero)
                {
                    /* Take right extreme. */
                    result = lines[lineNo].point + tRight * lines[lineNo].direction;
                }
                else
                {
                    /* Take left extreme. */
                    result = lines[lineNo].point + tLeft * lines[lineNo].direction;
                }
            }
            else
            {
                /* Optimize closest point. */
                sfloat t = lines[lineNo].direction * (optVelocity - lines[lineNo].point);

                if (t < tLeft)
                {
                    result = lines[lineNo].point + tLeft * lines[lineNo].direction;
                }
                else if (t > tRight)
                {
                    result = lines[lineNo].point + tRight * lines[lineNo].direction;
                }
                else
                {
                    result = lines[lineNo].point + t * lines[lineNo].direction;
                }
            }

            return true;
        }

        /**
         * <summary>Solves a two-dimensional linear program subject to linear
         * constraints defined by lines and a circular constraint.</summary>
         *
         * <returns>The number of the line it fails on, and the number of lines
         * if successful.</returns>
         *
         * <param name="lines">Lines defining the linear constraints.</param>
         * <param name="radius">The radius of the circular constraint.</param>
         * <param name="optVelocity">The optimization velocity.</param>
         * <param name="directionOpt">True if the direction should be optimized.
         * </param>
         * <param name="result">A reference to the result of the linear program.
         * </param>
         */
        private int linearProgram2(IList<Line> lines, sfloat radius, Vector2 optVelocity, bool directionOpt, ref Vector2 result)
        {
            if (directionOpt)
            {
                /*
                 * Optimize direction. Note that the optimization velocity is of
                 * unit length in this case.
                 */
                result = optVelocity * radius;
            }
            else if (RVOMath.absSq(optVelocity) > RVOMath.sqr(radius))
            {
                /* Optimize closest point and outside circle. */
                result = RVOMath.normalize(optVelocity) * radius;
            }
            else
            {
                /* Optimize closest point and inside circle. */
                result = optVelocity;
            }

            for (int i = 0; i < lines.Count; ++i)
            {
                // 行列式计算result是否在直线安全的一边
                if (RVOMath.det(lines[i].direction, lines[i].point - result) > sfloat.Zero)
                {
                    /* Result does not satisfy constraint i. Compute new optimal result. */
                    Vector2 tempResult = result;
                    // 违反约束,尝试找到新的解。
                    if (!linearProgram1(lines, i, radius, optVelocity, directionOpt, ref result))
                    {
                        // 找不到解回退
                        result = tempResult;

                        return i;
                    }
                }
            }

            return lines.Count;
        }

        /**
         * <summary>Solves a two-dimensional linear program subject to linear
         * constraints defined by lines and a circular constraint.</summary>
         *
         * 在linearProgram2失败时提供一个后备解决方案
         * 通过创建一组原始约束的"投影",找到一个折中的解决方案。这个方法是启发式的
         *
         * <param name="lines">Lines defining the linear constraints.</param>
         * <param name="numObstLines">Count of obstacle lines.</param>
         * <param name="beginLine">The line on which the 2-d linear program
         * failed.</param>
         * <param name="radius">The radius of the circular constraint.</param>
         * <param name="result">A reference to the result of the linear program.
         * </param>
         */
        private void linearProgram3(IList<Line> lines, int numObstLines, int beginLine, sfloat radius, ref Vector2 result)
        {
            sfloat distance = sfloat.Zero;

            for (int i = beginLine; i < lines.Count; ++i)
            {
                if (RVOMath.det(lines[i].direction, lines[i].point - result) > distance)
                {
                    /* Result does not satisfy constraint of line i. */
                    IList<Line> projLines = new List<Line>();
                    for (int ii = 0; ii < numObstLines; ++ii)
                    {
                        projLines.Add(lines[ii]);
                    }

                    for (int j = numObstLines; j < i; ++j)
                    {
                        Line line;

                        sfloat determinant = RVOMath.det(lines[i].direction, lines[j].direction);

                        // 是否平行
                        if (RVOMath.fabs(determinant) <= RVOMath.RVO_EPSILON)
                        {
                            /* Line i and line j are parallel. */
                            if (lines[i].direction * lines[j].direction > sfloat.Zero)
                            {
                                /* Line i and line j point in the same direction. */
                                continue;
                            }
                            else
                            {
                                /* Line i and line j point in opposite direction. */
                                // 如果方向相反,取两线上的点的中点作为新线的点
                                line.point = (sfloat)0.5f * (lines[i].point + lines[j].point);
                            }
                        }
                        else
                        {
                            // 计算两条线的交点,作为新投影线的点。
                            line.point = lines[i].point + (RVOMath.det(lines[j].direction, lines[i].point - lines[j].point) / determinant) * lines[i].direction;
                        }

                        // 新方向实际上是原来两条线所形成的角的角平分线的垂直方向
                        line.direction = RVOMath.normalize(lines[j].direction - lines[i].direction);
                        // 新半平面包含了原来两条线之间的某个区域，并且扩展了新的区域，使得 linearProgram2 有可行解
                        projLines.Add(line);
                    }

                    Vector2 tempResult = result;
                    if (linearProgram2(projLines, radius, new Vector2(-lines[i].direction.y(), lines[i].direction.x()), true, ref result) < projLines.Count)
                    {
                        /*
                         * This should in principle not happen. The result is by
                         * definition already in the feasible region of this
                         * linear program. If it fails, it is due to small
                         * floating point error, and the current result is kept.
                         */
                        result = tempResult;
                    }

                    distance = RVOMath.det(lines[i].direction, lines[i].point - result);
                }
            }
        }
    }
}
