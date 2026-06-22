using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(RobotIKSolver))]
public class RobotIKSolverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(4);
        RobotIKSolver s = (RobotIKSolver)target;
        GUI.color = s.IsReachable ? Color.green : Color.red;
        GUILayout.Label(s.IsReachable ? "✔ 도달 가능" : "✘ 도달 불가");
        GUI.color = Color.white;
        if (GUILayout.Button("테스트: 캔버스 중심"))
            s.SetTarget(s.canvasCenter);
    }
}
#endif


/// <summary>
/// CCD(Cyclic Coordinate Descent) 기반 5DOF 역기구학.
///
/// 프레임당 제한된 반복 횟수로 자연스럽게 수렴 → 모든 관절이 함께 움직인다.
///
/// 관절별 회전축:
///   Arm1      — 월드 Y (방위각: 좌우)
///   Arm2      — 로컬 Z (고도각: 위아래) ← 핵심 변경
///   WristYaw  — 로컬 Y (방향 보정)
///   WristPitch — 로컬 X (붓 기울기, CCD 제외)
///   Slide     — 로컬 Y 선형 이동 (높이 보정)
/// </summary>
public class RobotIKSolver : MonoBehaviour
{
    // ── 관절 Transform ────────────────────────────────────────────────────────
    // 기존 RobotController의 슬롯이 여기로 이동됨
    [Header("관절 Transform")]
    public Transform arm1;        // Y축 회전 (방위각)
    public Transform arm2;        // Z축 회전 (고도각) ← Arm2 역할 변경
    public Transform wristYaw;    // Y축 회전 (방향 보정)
    public Transform wristPitch;  // X축 회전 (붓 기울기)
    public Transform slide;       // Y축 선형 이동
    public Transform brushTip;    // 붓끝 (IK 목표점)

    // ── CCD 설정 ──────────────────────────────────────────────────────────────
    [Header("CCD 설정")]
    [Range(1, 10)]
    [Tooltip("프레임당 반복 횟수. 낮을수록 부드럽고 천천히, 높을수록 빠르게 수렴")]
    public int iterationsPerFrame = 3;
    [Tooltip("붓끝이 목표로부터 이 거리 이하면 수렴 완료로 판단")]
    public float tolerance = 0.05f;

    // ── 관절 한계 ─────────────────────────────────────────────────────────────
    [Header("관절 한계 (도)")]
    public float arm1Min  = -170f; public float arm1Max  = 170f; // 좌우 방위각
    public float arm2Min  =  -80f; public float arm2Max  =  80f; // 상하 고도각
    public float yawMin   = -180f; public float yawMax   = 180f; // 손목 방향
    public float pitchMin =  -90f; public float pitchMax = -10f; // 붓 기울기

    // ── 슬라이드 ──────────────────────────────────────────────────────────────
    [Header("슬라이드 (높이 보정)")]
    public float slideBaseY = 3.2f;
    public float slideMin   = -1.3f;
    public float slideMax   =  2.2f;
    public float slideSpeed = 6f;

    // ── 캔버스 ────────────────────────────────────────────────────────────────
    [Header("캔버스 (월드 좌표)")]
    public Vector3 canvasCenter = new Vector3(0f, 3f, 8.5f);
    public float   canvasWidth  = 4f;
    public float   canvasHeight = 3f;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────
    private Vector3 _target;
    private float   _pitchDeg = -90f;
    private bool    _hasTarget;

    public bool IsReachable { get; private set; }


    /// <summary>
    /// 목표 위치 설정. 다음 Update()부터 CCD가 이 목표로 수렴한다.
    /// </summary>
    public void SetTarget(Vector3 worldPos, float pitchDeg = -90f)
    {
        _target    = ClampToCanvas(worldPos);
        _pitchDeg  = Mathf.Clamp(pitchDeg, pitchMin, pitchMax);
        _hasTarget = true;
    }

    void Update()
    {
        if (!_hasTarget || brushTip == null) return;

        // CCD: 프레임당 iterationsPerFrame번만 실행
        // → 한 번에 수렴하지 않고 조금씩 가까워짐 = 자연스러운 관절 움직임
        for (int i = 0; i < iterationsPerFrame; i++)
        {
            float dist = Vector3.Distance(brushTip.position, _target);
            IsReachable = dist < 1.5f;
            if (dist < tolerance) break;
            CCDPass(_target);
        }

        // WristPitch: 붓 기울기 직접 설정 (위치 IK와 분리)
        if (wristPitch)
            wristPitch.localEulerAngles = new Vector3(_pitchDeg, 0f, 0f);

        // Slide: 남은 Y 오차를 슬라이드로 보정
        if (slide)
        {
            float d   = Mathf.Clamp(slideBaseY - _target.y, slideMin, slideMax);
            Vector3 p = slide.localPosition;
            p.y = Mathf.Lerp(p.y, -d, Time.deltaTime * slideSpeed);
            slide.localPosition = p;
        }
    }

    /// <summary>
    /// CCD 한 패스.
    /// WristYaw → Arm2 → Arm1 순서 (끝 관절부터 기저 관절로).
    /// </summary>
    void CCDPass(Vector3 target)
    {
        // 1. WristYaw: 로컬 Y축 회전 (방향 보정)
        RotateToward(wristYaw, target, wristYaw.up);
        SingleAxisClamp(wristYaw, 'Y', yawMin, yawMax);

        // 2. Arm2: 로컬 Z축 회전 (고도각 — 팔을 위아래로 꺾음)
        RotateToward(arm2, target, arm2.forward);
        SingleAxisClamp(arm2, 'Z', arm2Min, arm2Max);

        // 3. Arm1: 월드 Y축 회전 (방위각 — 팔을 좌우로 돌림)
        RotateToward(arm1, target, Vector3.up);
        SingleAxisClamp(arm1, 'Y', arm1Min, arm1Max);
    }

    /// <summary>
    /// joint를 rotAxis 기준으로 회전시켜 brushTip이 target을 향하도록 한다.
    /// </summary>
    void RotateToward(Transform joint, Vector3 target, Vector3 rotAxis)
    {
        if (joint == null) return;

        Vector3 toEnd    = brushTip.position - joint.position;
        Vector3 toTarget = target            - joint.position;

        // 회전축에 수직인 평면에 투영
        toEnd    = Vector3.ProjectOnPlane(toEnd,    rotAxis);
        toTarget = Vector3.ProjectOnPlane(toTarget, rotAxis);

        if (toEnd.magnitude < 0.001f || toTarget.magnitude < 0.001f) return;

        float angle = Vector3.SignedAngle(toEnd, toTarget, rotAxis);
        joint.Rotate(rotAxis, angle, Space.World);
    }

    /// <summary>
    /// 관절을 단일 축 회전만 허용하고 각도를 min/max 범위로 클램프한다.
    /// </summary>
    void SingleAxisClamp(Transform joint, char axis, float min, float max)
    {
        if (joint == null) return;
        Vector3 e = joint.localEulerAngles;

        switch (axis)
        {
            case 'Y':
                float y = e.y > 180f ? e.y - 360f : e.y;
                joint.localEulerAngles = new Vector3(0f, Mathf.Clamp(y, min, max), 0f);
                break;
            case 'Z':
                float z = e.z > 180f ? e.z - 360f : e.z;
                joint.localEulerAngles = new Vector3(0f, 0f, Mathf.Clamp(z, min, max));
                break;
            case 'X':
                float x = e.x > 180f ? e.x - 360f : e.x;
                joint.localEulerAngles = new Vector3(Mathf.Clamp(x, min, max), 0f, 0f);
                break;
        }
    }

    /// <summary>캔버스 XY 범위 클램프 + Z 고정.</summary>
    public Vector3 ClampToCanvas(Vector3 pos)
    {
        return new Vector3(
            Mathf.Clamp(pos.x, canvasCenter.x - canvasWidth  * 0.5f,
                                canvasCenter.x + canvasWidth  * 0.5f),
            Mathf.Clamp(pos.y, canvasCenter.y - canvasHeight * 0.5f,
                                canvasCenter.y + canvasHeight * 0.5f),
            canvasCenter.z
        );
    }


    // ── Gizmo ─────────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        DrawCanvasGizmo();
        if (_hasTarget)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_target, 0.1f);
        }
    }

    void DrawCanvasGizmo()
    {
        float hw = canvasWidth * 0.5f, hh = canvasHeight * 0.5f, z = canvasCenter.z;
        Vector3 tl = new Vector3(canvasCenter.x - hw, canvasCenter.y + hh, z);
        Vector3 tr = new Vector3(canvasCenter.x + hw, canvasCenter.y + hh, z);
        Vector3 bl = new Vector3(canvasCenter.x - hw, canvasCenter.y - hh, z);
        Vector3 br = new Vector3(canvasCenter.x + hw, canvasCenter.y - hh, z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(tl, tr); Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl); Gizmos.DrawLine(bl, tl);
        Gizmos.color = new Color(1f, 1f, 0f, 0.04f);
        Gizmos.DrawCube(canvasCenter, new Vector3(canvasWidth, canvasHeight, 0.02f));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(tl + Vector3.up * 0.2f, $"캔버스 {canvasWidth}×{canvasHeight}");
#endif
    }
}