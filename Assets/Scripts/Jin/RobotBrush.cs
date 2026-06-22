using UnityEngine;

/// <summary>
/// BrushTip 오브젝트에 부착하는 스크립트.
/// 추상화(다색 붓터치 겹침) 그림 전용 — AbstractStrokeBrush 단일 브러시만 사용.
/// </summary>
public class RobotBrush : MonoBehaviour
{
    [Header("연결 설정")]
    public CanvasPainter canvasPainter;

    [Header("브러시 머티리얼")]
    [Tooltip("Custom/AbstractStrokeBrush 셰이더가 적용된 머티리얼")]
    public Material brushMaterial;

    [Header("붓 설정")]
    public float brushLength = 0.5f;

    [Header("필압 설정")]
    [Range(0.1f, 3.0f)] public float pressureSensitivity = 1.0f;
    [Range(0f, 0.5f)] public float minPressureThreshold = 0.05f;

    [Header("지우개 설정")]
    public Material eraserMaterial;
    [Tooltip("지우개 효과 후 inkMap도 함께 지울지 여부")]
    public bool eraseInkMap = true;
    public bool isEraserMode = false;

    // ─────────────────────────────────────────────────────────────
    // 외부 읽기용 프로퍼티
    // ─────────────────────────────────────────────────────────────
    public bool IsTouching { get; private set; }
    public Vector2 LastPaintedUV { get; private set; }
    public float CurrentPressure { get; private set; }

    // ─────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────
    void Update()
    {
        IsTouching = false;
        CurrentPressure = 0f;

        if (canvasPainter == null)
        {
            Debug.LogWarning("RobotBrush: CanvasPainter가 연결되지 않았습니다!");
            return;
        }

        Vector3 rayDir = transform.up;

        if (!Physics.Raycast(transform.position, rayDir, out RaycastHit hit, brushLength))
        {
            Debug.DrawRay(transform.position, rayDir * brushLength, Color.red);
            return;
        }

        CanvasPainter canvas = hit.collider.GetComponent<CanvasPainter>();
        if (canvas == null)
        {
            Debug.DrawRay(transform.position, rayDir * brushLength, Color.red);
            return;
        }

        float rawPressure = 1f - Mathf.Clamp01(hit.distance / brushLength);
        float pressure = Mathf.Clamp01(rawPressure * pressureSensitivity);

        if (pressure < minPressureThreshold)
        {
            Debug.DrawRay(transform.position, rayDir * brushLength, Color.yellow);
            return;
        }

        IsTouching = true;
        LastPaintedUV = hit.textureCoord;
        CurrentPressure = pressure;

        if (isEraserMode)
            PaintEraser(canvas, hit.textureCoord, pressure);
        else
            PaintStroke(canvas, hit.textureCoord, pressure);

        Debug.DrawRay(transform.position, rayDir * brushLength, Color.green);
    }

    // ─────────────────────────────────────────────────────────────
    // 메인 브러시 — AbstractStrokeBrush
    // ─────────────────────────────────────────────────────────────
    private void PaintStroke(CanvasPainter canvas, Vector2 uv, float pressure)
    {
        if (brushMaterial == null) return;

        brushMaterial.SetVector("_BrushUV", new Vector4(uv.x, uv.y, 0, 0));
        brushMaterial.SetFloat("_Pressure", pressure);

        canvas.Paint(uv, brushMaterial, pressure);
    }

    // ─────────────────────────────────────────────────────────────
    // 지우개
    // ─────────────────────────────────────────────────────────────
    private void PaintEraser(CanvasPainter canvas, Vector2 uv, float pressure)
    {
        if (eraserMaterial == null) return;

        eraserMaterial.SetVector("_BrushUV", new Vector4(uv.x, uv.y, 0, 0));
        eraserMaterial.SetFloat("_Pressure", pressure);

        canvas.PaintOnly(uv, eraserMaterial, pressure);

        if (eraseInkMap)
        {
            float eraseRadius = eraserMaterial.HasProperty("_BrushSize")
                ? eraserMaterial.GetFloat("_BrushSize") * Mathf.Lerp(0.3f, 1.0f, pressure)
                : 0.03f;
            canvas.EraseInkAtUV(uv, eraseRadius);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 외부 스크립트용 공개 메서드
    // ─────────────────────────────────────────────────────────────
    public void SetColor(Color color)
    {
        if (brushMaterial != null) brushMaterial.SetColor("_BrushColor", color);
    }

    public void SetEraserMode(bool enable) => isEraserMode = enable;
}