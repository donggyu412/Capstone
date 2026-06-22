using System.Collections.Generic;
using UnityEngine;

// ── 펜타블렛 필압을 사용하려면 New Input System 패키지가 필요합니다 ──
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MousePainter : MonoBehaviour
{
    #region 1. 변수 및 설정

    [Header("페인팅 기본 설정")]
    public CanvasPainter canvasPainter;
    public Material[] brushes;
    private int currentBrushIndex = 0;
    private Vector2 lastUV;

    [Header("브러시 인덱스 설정 (Inspector 기준)")]
    public int inkBrushIndex        = 1;  // BrushInk
    public int sprayBrushIndex      = 3;  // BrushSpray
    public int blurBrushIndex       = 4;  // BrushBlur
    public int evaporatingBrushIndex= 5;  // BrushEvaporatingMaterial
    public int eraserBrushIndex     = 6;  // BrushEraser (신규)

    [Header("스프레이 브러시 설정")]
    public int   sprayParticleCount = 15;
    public float sprayRadius        = 0.05f;

    [Tooltip("스프레이 활성화 여부 (키 T로 토글, false면 분사 안 함)")]
    public bool isSprayActive = true;

    [Header("흐름(Drip) 효과 설정")]
    public Material dripTrailMaterial;
    private bool isDripEnabled = true;

    [Header("기화펜 효과 설정")]
    public Material brushEvaporatingMaterial;
    public float evaporateDuration     = 500f;
    public Color canvasBackgroundColor = Color.white;

    [Header("잉크 시뮬레이션 테스트 설정")]
    [Tooltip("체크하면 펜을 제자리에 누르고 있을 때도 잉크가 누적됩니다")]
    public bool enableStationaryInkAccumulation = true;

    [Tooltip("필압 표시 GUI (테스트용, 배포 시 끄세요)")]
    public bool showPressureDebugGUI = true;

    [Header("필압 리매핑")]
    [Tooltip("타블렛 실제 최대 압력값. 콘솔 [펜타블렛] 로그에서 최대치 확인 후 설정.")]
    [Range(0.05f, 1.0f)]
    public float maxExpectedPressure = 0.2f;

    [HideInInspector] public float currentPressure = 0f;
    private bool usesPressure = false;
    private bool wasPaintingLastFrame = false;
    private Vector2 stationaryUV;

    private List<EvaporatingPoint> activeEvaporatingPoints = new List<EvaporatingPoint>();
    private List<Drip> activeDrips = new List<Drip>();

    #endregion

    #region 2. 내부 데이터 클래스

    private class EvaporatingPoint
    {
        public Vector2  uv;
        public float    life, maxLife;
        public Color    color;
        public Material materialInstance;

        public EvaporatingPoint(Vector2 uv, Color color, float duration, Material mat)
        {
            this.uv = uv; this.color = color;
            this.maxLife = duration; this.life = duration;
            this.materialInstance = mat;
        }
    }

    private class Drip
    {
        public Vector2 uv;
        public float   life, speed;
        public Color   color;
    }

    #endregion

    #region 3. Unity 라이프사이클

    void Update()
    {
        HandleBrushSelection();
        HandleDripToggle();
        HandleSprayToggle();

        if (isDripEnabled) UpdateDrips();
        UpdateEvaporation();

        if (canvasPainter == null) return;
        HandleMouseDrawing();
    }

    void OnGUI()
    {
        if (!showPressureDebugGUI) return;

        GUI.Box(new Rect(10, 10, 240, 90), "");
        GUI.Label(new Rect(15, 15, 220, 20), $"필압(Pressure): {currentPressure:F3}");

        GUI.Box(new Rect(15, 38, 200, 18), "");
        Color prev = GUI.color;
        GUI.color = Color.Lerp(Color.green, Color.red, currentPressure);
        GUI.Box(new Rect(15, 38, 200 * currentPressure, 18), "");
        GUI.color = prev;

        string brushName = usesPressure
            ? $"브러시 {currentBrushIndex} (필압 활성)"
            : "기타 (필압 비활성)";
        GUI.Label(new Rect(15, 58, 220, 20), $"브러시: {brushName}");

        // 스프레이 ON/OFF 상태 표시
        if (currentBrushIndex == sprayBrushIndex)
        {
            GUI.color = isSprayActive ? Color.green : Color.red;
            GUI.Label(new Rect(15, 75, 220, 18),
                isSprayActive ? "스프레이: ON (T키로 끄기)" : "스프레이: OFF (T키로 켜기)");
            GUI.color = Color.white;
        }
    }

    #endregion

    #region 4. 필압 읽기

    void Start()
    {
    #if ENABLE_INPUT_SYSTEM
        foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
            Debug.Log($"[장치] {device.name} / 타입: {device.GetType().Name}");
    #endif
    }

    private float GetPenPressure()
    {
    #if ENABLE_INPUT_SYSTEM
        if (Pen.current != null)
        {
            float raw = Pen.current.pressure.ReadValue();
            if (raw > 0.01f)
            {
                float remapped = Mathf.Clamp01(raw / maxExpectedPressure);
                Debug.Log($"[펜타블렛] 원본:{raw:F3} -> 리매핑:{remapped:F3}");
                return remapped;
            }
        }
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            Debug.Log("[마우스] 필압: 1.000");
            return 1.0f;
        }
        return 0f;
    #else
        return Input.GetMouseButton(0) ? 1.0f : 0f;
    #endif
    }

    #endregion

    #region 5. 입력 처리

    private void HandleBrushSelection()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) currentBrushIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentBrushIndex = inkBrushIndex;
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentBrushIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) currentBrushIndex = sprayBrushIndex;
        if (Input.GetKeyDown(KeyCode.Alpha5)) currentBrushIndex = blurBrushIndex;
        if (Input.GetKeyDown(KeyCode.Alpha6)) currentBrushIndex = evaporatingBrushIndex;
        if (Input.GetKeyDown(KeyCode.Alpha7)) currentBrushIndex = eraserBrushIndex; // 신규: 7번 키
    }

    private void HandleDripToggle()
    {
        if (Input.GetKeyDown(KeyCode.O)) isDripEnabled = false;
        if (Input.GetKeyDown(KeyCode.P)) isDripEnabled = true;
    }

    private void HandleSprayToggle()
    {
        // T키로 스프레이 ON/OFF 토글
        if (Input.GetKeyDown(KeyCode.T))
        {
            isSprayActive = !isSprayActive;
            Debug.Log($"[스프레이] {(isSprayActive ? "ON" : "OFF")}");
        }
    }

    private void HandleMouseDrawing()
    {
        // 필압 적용 브러시: Material(0), InkBrush(1), Eraser(6)
        usesPressure = (currentBrushIndex == 0
                     || currentBrushIndex == inkBrushIndex
                     || currentBrushIndex == eraserBrushIndex);
        currentPressure = usesPressure ? GetPenPressure() : 1.0f;

        if (usesPressure && Input.GetMouseButton(0))
            Debug.Log($"[필압] {currentPressure:F3}  브러시: {currentBrushIndex}");

        // ── 클릭 시작 ──────────────────────────────────────────────
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                lastUV       = hit.textureCoord;
                stationaryUV = lastUV;

                if (currentBrushIndex == eraserBrushIndex)
                    EraseAtUV(lastUV, currentPressure);
                else
                    canvasPainter.Paint(lastUV, brushes[currentBrushIndex], currentPressure);

                if (currentBrushIndex == evaporatingBrushIndex)
                    AddEvaporatingPoint(lastUV);

                wasPaintingLastFrame = true;
            }
        }
        // ── 드래그 중 ──────────────────────────────────────────────
        else if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector2 currentUV = hit.textureCoord;
                bool moved = Vector2.Distance(lastUV, currentUV) > 0.0005f;

                if (currentBrushIndex == sprayBrushIndex)
                {
                    // 스프레이: isSprayActive가 false면 아무것도 안 함
                    if (isSprayActive)
                        PaintWithSpray(currentUV);
                }
                else if (currentBrushIndex == inkBrushIndex)
                {
                    if (moved)
                    {
                        PaintLine(lastUV, currentUV, currentPressure);
                        if (isDripEnabled && Random.value < 0.05f)
                            CreateDrip(currentUV);
                    }
                    else if (enableStationaryInkAccumulation)
                        canvasPainter.AccumulateInkOnly(currentUV, currentPressure);
                }
                else if (currentBrushIndex == evaporatingBrushIndex)
                {
                    PaintLine(lastUV, currentUV, 1.0f);
                    if (Random.value < 0.2f) AddEvaporatingPoint(currentUV);
                }
                else if (currentBrushIndex == eraserBrushIndex)
                {
                    // 지우개: 이동 시 선형 보간, 정지 시 단일 포인트
                    if (moved)
                        EraseLine(lastUV, currentUV, currentPressure);
                    else
                        EraseAtUV(currentUV, currentPressure);
                }
                else
                {
                    if (moved)
                        PaintLine(lastUV, currentUV, currentPressure);
                    else if (enableStationaryInkAccumulation)
                        canvasPainter.AccumulateInkOnly(currentUV, currentPressure);
                }

                lastUV       = currentUV;
                stationaryUV = currentUV;
                wasPaintingLastFrame = true;
            }
        }
        else
        {
            currentPressure      = 0f;
            wasPaintingLastFrame = false;
        }
    }

    #endregion

    #region 6. 기본 드로잉

    private void PaintLine(Vector2 start, Vector2 end, float pressure)
    {
        float dist = Vector2.Distance(start, end);
        if (dist > 0.002f)
        {
            int steps = Mathf.CeilToInt(dist / 0.002f);
            for (int i = 1; i <= steps; i++)
            {
                Vector2 uv = Vector2.Lerp(start, end, (float)i / steps);
                canvasPainter.Paint(uv, brushes[currentBrushIndex], pressure);
            }
        }
    }

    private Color GetCurrentBrushColor()
    {
        Material mat = brushes[currentBrushIndex];
        if (mat.HasProperty("_BrushColor")) return mat.GetColor("_BrushColor");
        return Color.white;
    }

    #endregion

    #region 7. 스프레이 효과

    private void PaintWithSpray(Vector2 centerUV)
    {
        if (brushes[sprayBrushIndex] == null) return;
        Material mat = brushes[sprayBrushIndex];

        for (int i = 0; i < sprayParticleCount; i++)
        {
            Vector2 offset   = Random.insideUnitCircle * sprayRadius;
            Vector2 sprayUV  = centerUV + offset;
            mat.SetVector("_BrushUV", new Vector4(sprayUV.x, sprayUV.y, 0, 0));

            // PaintOnly 사용 → inkMap 누적 없음 (스프레이 잉크 번짐 방지)
            canvasPainter.PaintOnly(sprayUV, mat, 1.0f);
        }
    }

    #endregion

    #region 8. 기화펜 효과

    private void AddEvaporatingPoint(Vector2 uv)
    {
        if (brushEvaporatingMaterial == null) return;
        Color c = GetCurrentBrushColor();
        Material tempMat = new Material(brushEvaporatingMaterial);
        tempMat.SetVector("_BrushUV",  new Vector4(uv.x, uv.y, 0, 0));
        tempMat.SetColor("_BrushColor", c);
        activeEvaporatingPoints.Add(new EvaporatingPoint(uv, c, evaporateDuration, tempMat));
    }

    private void UpdateEvaporation()
    {
        if (canvasPainter == null) return;
        for (int i = activeEvaporatingPoints.Count - 1; i >= 0; i--)
        {
            EvaporatingPoint p = activeEvaporatingPoints[i];
            p.life--;
            float alpha = (float)p.life / (float)p.maxLife;
            if (p.materialInstance != null)
            {
                Color fadeColor = Color.Lerp(canvasBackgroundColor, p.color, alpha);
                p.materialInstance.SetColor("_BrushColor", fadeColor);
                canvasPainter.Paint(p.uv, p.materialInstance, 1.0f);
            }
            if (p.life <= 0)
            {
                if (p.materialInstance != null) Destroy(p.materialInstance);
                activeEvaporatingPoints.RemoveAt(i);
            }
        }
    }

    #endregion

    #region 9. 지우개 효과 (신규)

    /// <summary>두 UV 사이를 지우개로 선형 보간하며 지웁니다.</summary>
    private void EraseLine(Vector2 start, Vector2 end, float pressure)
    {
        float dist = Vector2.Distance(start, end);
        if (dist > 0.002f)
        {
            int steps = Mathf.CeilToInt(dist / 0.002f);
            for (int i = 1; i <= steps; i++)
            {
                Vector2 uv = Vector2.Lerp(start, end, (float)i / steps);
                EraseAtUV(uv, pressure);
            }
        }
        else
            EraseAtUV(end, pressure);
    }

    /// <summary>
    /// UV 한 지점을 지웁니다.
    /// 1) EraserShader로 배경색 덧칠 (시각적 지우기)
    /// 2) EraseInkAtUV로 inkMap도 초기화 (잉크 재발생 방지)
    /// </summary>
    private void EraseAtUV(Vector2 uv, float pressure)
    {
        if (brushes == null || eraserBrushIndex >= brushes.Length) return;
        Material mat = brushes[eraserBrushIndex];
        if (mat == null) return;

        mat.SetVector("_BrushUV", new Vector4(uv.x, uv.y, 0, 0));
        mat.SetFloat("_Pressure", pressure);

        // 시각적 지우기 (inkMap 누적 없음)
        canvasPainter.PaintOnly(uv, mat, pressure);

        // inkMap 지우기 (잉크 흘러내림 재발생 방지)
        float eraseRadius = mat.HasProperty("_BrushSize")
            ? mat.GetFloat("_BrushSize") * Mathf.Lerp(0.3f, 1.0f, pressure)
            : 0.03f;
        canvasPainter.EraseInkAtUV(uv, eraseRadius);
    }

    #endregion

    #region 10. 흐름 효과

    private void CreateDrip(Vector2 startUV)
    {
        activeDrips.Add(new Drip
        {
            uv    = startUV,
            life  = Random.Range(50, 5000),
            speed = Random.Range(0.00008f, 0.0000001f),
            color = GetCurrentBrushColor()
        });
    }

    private void UpdateDrips()
    {
        if (canvasPainter == null) return;
        for (int i = activeDrips.Count - 1; i >= 0; i--)
        {
            Drip drip = activeDrips[i];
            drip.uv.x -= drip.speed;
            if (dripTrailMaterial != null)
            {
                if (dripTrailMaterial.HasProperty("_BrushColor"))
                    dripTrailMaterial.SetColor("_BrushColor", drip.color);
                canvasPainter.Paint(drip.uv, dripTrailMaterial, 1.0f);
            }
            drip.life--;
            if (drip.life <= 0) activeDrips.RemoveAt(i);
        }
    }

    #endregion
}
