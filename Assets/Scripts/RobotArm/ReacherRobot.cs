using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class ReacherRobot : Agent
{
    public GameObject pendulumA; // j1
    public GameObject pendulumB; // j2
    public GameObject pendulumC; // j3
    public GameObject pendulumD; // j4
    public GameObject pendulumE; // j5
    public GameObject pendulumF; // j6

    Rigidbody m_RbA, m_RbB, m_RbC, m_RbD, m_RbE, m_RbF;

    public GameObject brushTip;
    public GameObject canvas;
    public DrawOnCanvas drawCanvas;

    public float torqueStrength = 150f;

    // 범위 기록
    Vector3 minBounds = new Vector3(float.MaxValue,  float.MaxValue,  float.MaxValue);
    Vector3 maxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);
    float previousArea = 0f;

    public override void Initialize()
    {
        m_RbA = pendulumA.GetComponent<Rigidbody>();
        m_RbB = pendulumB.GetComponent<Rigidbody>();
        m_RbC = pendulumC.GetComponent<Rigidbody>();
        m_RbD = pendulumD.GetComponent<Rigidbody>();
        m_RbE = pendulumE.GetComponent<Rigidbody>();
        m_RbF = pendulumF.GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        // 관절 초기화
        ResetJoint(pendulumA, m_RbA, new Vector3(-1.937307f, 0.6f,   1.24f));
        ResetJoint(pendulumB, m_RbB, new Vector3(-2.087307f, 0.6f,   1.24f));
        ResetJoint(pendulumC, m_RbC, new Vector3(-2.087307f, 1.425f, 1.24f));
        ResetJoint(pendulumD, m_RbD, new Vector3(-2.087307f, 1.425f, 1.24f));
        ResetJoint(pendulumE, m_RbE, new Vector3(-2.087307f, 2.05f,  1.24f));
        ResetJoint(pendulumF, m_RbF, new Vector3(-2.087307f, 2.16f,  1.24f));

        minBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        maxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        previousArea = 0f;
    }

    void ResetJoint(GameObject joint, Rigidbody rb, Vector3 localPos)
    {
        joint.transform.position = localPos + transform.position;
        joint.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(pendulumA.transform.localRotation); // 4
        sensor.AddObservation(pendulumB.transform.localRotation); // 4
        sensor.AddObservation(pendulumC.transform.localRotation); // 4
        sensor.AddObservation(pendulumD.transform.localRotation); // 4
        sensor.AddObservation(pendulumE.transform.localRotation); // 4
        sensor.AddObservation(pendulumF.transform.localRotation); // 4

        sensor.AddObservation(m_RbA.angularVelocity); // 3
        sensor.AddObservation(m_RbB.angularVelocity); // 3
        sensor.AddObservation(m_RbC.angularVelocity); // 3
        sensor.AddObservation(m_RbD.angularVelocity); // 3
        sensor.AddObservation(m_RbE.angularVelocity); // 3
        sensor.AddObservation(m_RbF.angularVelocity); // 3

        sensor.AddObservation(brushTip.transform.localPosition); // 3

        Vector3 currentSize = Vector3.zero;
        if (minBounds.x != float.MaxValue)
            currentSize = maxBounds - minBounds;
        sensor.AddObservation(currentSize); // 3

        // 총합 = 46
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var act = actions.ContinuousActions;

        m_RbA.AddTorque(pendulumA.transform.forward * act[0] * torqueStrength);
        m_RbB.AddTorque(pendulumB.transform.up      * act[1] * torqueStrength);
        m_RbC.AddTorque(pendulumC.transform.up      * act[2] * torqueStrength);
        m_RbD.AddTorque(pendulumD.transform.forward * act[3] * torqueStrength);
        m_RbE.AddTorque(pendulumE.transform.up      * act[4] * torqueStrength);
        m_RbF.AddTorque(pendulumF.transform.forward * act[5] * torqueStrength);

        // 바닥 뚫기 & 박살 감지 (10step 이후부터만 체크)
        if (StepCount > 10)
        {
            bool fallen =
                brushTip.transform.position.y < 0f ||
                pendulumA.transform.position.y < 0f ||
                pendulumB.transform.position.y < 0f ||
                pendulumC.transform.position.y < 0f ||
                pendulumD.transform.position.y < 0f ||
                pendulumE.transform.position.y < 0f ||
                pendulumF.transform.position.y < 0f;

            if (fallen)
            {
                AddReward(-1.0f);
                EndEpisode();
                return;
            }
        }

        // BrushTip 범위 업데이트
        Vector3 pos = brushTip.transform.position;
        minBounds = Vector3.Min(minBounds, pos);
        maxBounds = Vector3.Max(maxBounds, pos);

        // 새 범위 탐색할수록 reward
        float currentArea = (maxBounds - minBounds).magnitude;
        float areaGain = currentArea - previousArea;
        if (areaGain > 0)
            AddReward(areaGain * 1.0f);
        previousArea = currentArea;

        // 관절 속도 패널티
        foreach (var rb in new[] { m_RbA, m_RbB, m_RbC, m_RbD, m_RbE, m_RbF })
        {
            if (rb.angularVelocity.magnitude > 10f)
                AddReward(-0.05f);
        }
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var act = actionsOut.ContinuousActions;
        act[0] = 0f;
        act[1] = 0f;
        act[2] = 0f;
        act[3] = 0f;
        act[4] = 0f;
        act[5] = 0f;
    }

    void OnDrawGizmos()
    {
        if (minBounds.x == float.MaxValue) return;

        Vector3 center = (minBounds + maxBounds) / 2f;
        Vector3 size   = maxBounds - minBounds;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);

        if (brushTip != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(brushTip.transform.position, 0.05f);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(minBounds, 0.05f);
        Gizmos.DrawSphere(maxBounds, 0.05f);
    }
}