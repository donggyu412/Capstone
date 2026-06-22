using UnityEngine;

public class RobotController : MonoBehaviour
{
    [Header("참조")]
    public RobotIKSolver ikSolver;

    void Start()
    {
        if (ikSolver == null)
            ikSolver = GetComponent<RobotIKSolver>();

        if (ikSolver == null)
            Debug.LogError("[RobotController] RobotIKSolver를 찾을 수 없습니다!");
    }

    public void SetTarget(Vector3 worldPos, float yawDeg = 0f, float pitchDeg = -90f)
    {
        ikSolver.SetTarget(worldPos, pitchDeg);
    }
}