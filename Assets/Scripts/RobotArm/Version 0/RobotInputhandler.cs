using UnityEngine;

/// <summary>
/// 마우스/키보드 입력을 RobotController에 전달한다.
///
/// [키보드]
///   A/D — 캔버스 좌우 (X축)
///   W/S — 캔버스 상하 (Y축)
///   Q/E — 붓 기울기 (PITCH)
///
/// [마우스] 캔버스 Collider 레이캐스트 → 붓끝 목표
/// </summary>
public class RobotInputHandler : MonoBehaviour
{
    [Header("참조")]
    public RobotController robotController;
    public RobotIKSolver   ikSolver;
    public Collider        canvasCollider;
    public Camera          mainCamera;      // 마우스 모드에서만 필요. 키보드만 쓴다면 비워도 됨

    [Header("키보드 속도")]
    public float moveSpeed  = 3f;
    public float pitchSpeed = 40f;

    [Header("입력 모드")]
    public InputMode mode = InputMode.Both;
    public enum InputMode { Mouse, Keyboard, Both }

    private Vector3 _target;
    private float   _pitch = -90f;


    void Start()
    {
        if (robotController == null) robotController = GetComponent<RobotController>();
        if (ikSolver == null)        ikSolver        = GetComponent<RobotIKSolver>();
        if (mainCamera == null)      mainCamera      = Camera.main;

        _target = ikSolver.canvasCenter;
    }

    void Update()
    {
        if (mode == InputMode.Mouse    || mode == InputMode.Both) UpdateMouse();
        if (mode == InputMode.Keyboard || mode == InputMode.Both) UpdateKeyboard();

        robotController.SetTarget(_target, 0f, _pitch);
    }

    void UpdateMouse()
    {
        if (canvasCollider == null || mainCamera == null) return;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (canvasCollider.Raycast(ray, out RaycastHit hit, 200f))
            _target = hit.point;
    }

    void UpdateKeyboard()
    {
        float dt = Time.deltaTime;

        // RobotRoot Y=-90 보정: A=오른쪽, D=왼쪽 반전
        if (Input.GetKey(KeyCode.A)) _target.x += moveSpeed * dt;
        if (Input.GetKey(KeyCode.D)) _target.x -= moveSpeed * dt;
        if (Input.GetKey(KeyCode.W)) _target.y += moveSpeed * dt;
        if (Input.GetKey(KeyCode.S)) _target.y -= moveSpeed * dt;

        if (Input.GetKey(KeyCode.Q)) _pitch -= pitchSpeed * dt;
        if (Input.GetKey(KeyCode.E)) _pitch += pitchSpeed * dt;
        _pitch = Mathf.Clamp(_pitch, ikSolver.pitchMin, ikSolver.pitchMax);

        _target = ikSolver.ClampToCanvas(_target);
    }
}