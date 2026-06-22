using UnityEngine;

[RequireComponent(typeof(CanvasMeshGenerator))]
public class CanvasPainter : MonoBehaviour
{
    [Header("Render Texture 설정")]
    public RenderTexture canvasRenderTexture;

    [Header("잉크 시뮬레이션 머티리얼")]
    public Material inkAddMaterial;
    public Material inkFlowMaterial;
    public Material inkDripMaterial;
    public Material inkEraseMaterial;

    [Header("잉크 물리 파라미터")]
    [Range(0.1f, 3.0f)] public float dripThreshold = 0.5f;
    [Range(0.0f, 2.0f)] public float flowSpeed = 0.5f;
    [Range(0.0f, 3.0f)] public float inkAccumulationRate = 0.8f;
    [Range(0.005f, 0.15f)] public float inkBrushRadius = 0.025f;
    [Range(0.0f, 0.2f)] public float inkDryRate = 0.01f;
    public Color inkColor = new Color(0.08f, 0.12f, 0.75f, 1f);

    private RenderTexture tempRenderTexture;
    private RenderTexture inkMapTexture;
    private RenderTexture inkMapTempTexture;

    // ─────────────────────────────────────────────────────────────
    // Awake
    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        inkAddMaterial = EnsureMaterial(inkAddMaterial, "InkAddMat");
        inkFlowMaterial = EnsureMaterial(inkFlowMaterial, "InkFlowMat");
        inkDripMaterial = EnsureMaterial(inkDripMaterial, "InkDripMat");

        if (inkEraseMaterial == null)
            inkEraseMaterial = FindMaterialByName("InkEraseMat");

        Debug.Log($"[CanvasPainter] Awake — " +
                  $"Add:{inkAddMaterial?.name ?? "NULL"} " +
                  $"Flow:{inkFlowMaterial?.name ?? "NULL"} " +
                  $"Drip:{inkDripMaterial?.name ?? "NULL"}");
    }

    private Material EnsureMaterial(Material mat, string targetName)
    {
        if (mat != null && mat.name == targetName) return mat;
        Material found = FindMaterialByName(targetName);
        if (found == null)
            Debug.LogWarning($"[CanvasPainter] '{targetName}' 머티리얼을 찾지 못했습니다.");
        return found;
    }

    private Material FindMaterialByName(string targetName)
    {
        foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            if (m.name == targetName) return m;
        return null;
    }

    // ─────────────────────────────────────────────────────────────
    // Start
    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        if (canvasRenderTexture == null)
        {
            Debug.LogError("CanvasPainter: canvasRenderTexture가 연결되지 않았습니다!");
            return;
        }

        int w = canvasRenderTexture.width;
        int h = canvasRenderTexture.height;

        tempRenderTexture = CreateRT(w, h, canvasRenderTexture.format);
        inkMapTexture = CreateRT(w, h, RenderTextureFormat.RFloat);
        inkMapTempTexture = CreateRT(w, h, RenderTextureFormat.RFloat);

        ClearCanvas();
    }

    // ─────────────────────────────────────────────────────────────
    // Update: ApplyInkToCanvas 비활성화 — 색상 재착색 문제 방지
    // ─────────────────────────────────────────────────────────────
    void Update()
    {
        if (inkMapTexture == null) return;
        SimulateInkFlow();
        // ApplyInkToCanvas(); ← 비활성화: inkColor 변경 시 캔버스 전체 재착색 방지
    }

    // ─────────────────────────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>캔버스에 획을 그리고 inkMap에 잉크를 누적합니다.</summary>
    public void Paint(Vector2 uv, Material brushMaterial, float pressure)
    {
        if (canvasRenderTexture == null || brushMaterial == null) return;

        brushMaterial.SetVector("_BrushUV", new Vector4(uv.x, uv.y, 0, 0));
        brushMaterial.SetFloat("_Pressure", pressure);

        Graphics.Blit(canvasRenderTexture, tempRenderTexture, brushMaterial);
        Graphics.Blit(tempRenderTexture, canvasRenderTexture);

        AddInkToMap(uv, pressure);
    }

    /// <summary>inkMap 누적 없이 캔버스에만 그립니다. (스프레이·블러·지우개 전용)</summary>
    public void PaintOnly(Vector2 uv, Material brushMaterial, float pressure)
    {
        if (canvasRenderTexture == null || brushMaterial == null) return;

        brushMaterial.SetVector("_BrushUV", new Vector4(uv.x, uv.y, 0, 0));
        brushMaterial.SetFloat("_Pressure", pressure);

        Graphics.Blit(canvasRenderTexture, tempRenderTexture, brushMaterial);
        Graphics.Blit(tempRenderTexture, canvasRenderTexture);
    }

    /// <summary>캔버스에 획은 안 그리고 inkMap에만 잉크를 누적합니다.</summary>
    public void AccumulateInkOnly(Vector2 uv, float pressure)
    {
        AddInkToMap(uv, pressure);
    }

    /// <summary>지우개 사용 시 inkMap의 특정 UV 위치 잉크를 제거합니다.</summary>
    public void EraseInkAtUV(Vector2 uv, float radius)
    {
        if (inkEraseMaterial == null || inkMapTexture == null) return;

        inkEraseMaterial.SetVector("_BrushUV", new Vector4(uv.x, uv.y, 0, 0));
        inkEraseMaterial.SetFloat("_EraseRadius", radius);

        Graphics.Blit(inkMapTexture, inkMapTempTexture, inkEraseMaterial);
        Graphics.Blit(inkMapTempTexture, inkMapTexture);
    }

    /// <summary>캔버스와 inkMap을 초기화합니다. (흰색으로 고정 초기화)</summary>
    public void ClearCanvas()
    {
        RenderTexture prev = RenderTexture.active;

        if (canvasRenderTexture != null)
        {
            RenderTexture.active = canvasRenderTexture;
            GL.Clear(true, true, Color.white);
        }

        if (inkMapTexture != null)
        {
            RenderTexture.active = inkMapTexture;
            GL.Clear(true, true, Color.black);
        }

        RenderTexture.active = prev;
    }

    /// <summary>
    /// 캔버스 텍스처 전체를 지정한 색으로 즉시 채웁니다.
    /// ClearCanvas()와 달리 inkMap(잉크 누적 정보)은 건드리지 않고
    /// 캔버스 시각적 배경색만 한 번에 깔아줍니다.
    /// Python stroke 기반 드로잉의 "타겟 배경색 칠하기" 용도로 사용.
    /// </summary>
    public void FillCanvasColor(Color color)
    {
        if (canvasRenderTexture == null) return;

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = canvasRenderTexture;
        GL.Clear(true, true, color);
        RenderTexture.active = prev;
    }

    public RenderTexture GetCanvasTexture() => canvasRenderTexture;
    public RenderTexture GetInkMapTexture() => inkMapTexture;

    // ─────────────────────────────────────────────────────────────
    // 내부 잉크 시뮬레이션
    // ─────────────────────────────────────────────────────────────

    private void AddInkToMap(Vector2 uv, float pressure)
    {
        if (inkAddMaterial == null || inkMapTexture == null) return;

        float addAmount = pressure * inkAccumulationRate * Time.deltaTime;

        inkAddMaterial.SetVector("_BrushUV", new Vector4(uv.x, uv.y, 0, 0));
        inkAddMaterial.SetFloat("_AddAmount", addAmount);
        inkAddMaterial.SetFloat("_BrushRadius", inkBrushRadius);

        Graphics.Blit(inkMapTexture, inkMapTempTexture, inkAddMaterial);
        Graphics.Blit(inkMapTempTexture, inkMapTexture);
    }

    private void SimulateInkFlow()
    {
        if (inkFlowMaterial == null || inkMapTexture == null) return;

        Vector3 worldGravity = Physics.gravity.normalized;
        float uGravity = Vector3.Dot(worldGravity, transform.right);
        float vGravity = Vector3.Dot(worldGravity, transform.up);
        Vector2 uvGravity = new Vector2(uGravity, vGravity);

        if (uvGravity.sqrMagnitude < 0.001f)
            uvGravity = new Vector2(0f, -0.01f);
        else
            uvGravity.Normalize();

        inkFlowMaterial.SetVector("_GravityUV", new Vector4(uvGravity.x, uvGravity.y, 0, 0));
        inkFlowMaterial.SetFloat("_DripThreshold", dripThreshold);
        inkFlowMaterial.SetFloat("_FlowSpeed", flowSpeed * Time.deltaTime);
        inkFlowMaterial.SetFloat("_DryRate", inkDryRate * Time.deltaTime);
        inkFlowMaterial.SetVector("_TexelSize", new Vector4(
            1f / inkMapTexture.width,
            1f / inkMapTexture.height,
            inkMapTexture.width,
            inkMapTexture.height));

        Graphics.Blit(inkMapTexture, inkMapTempTexture, inkFlowMaterial);
        Graphics.Blit(inkMapTempTexture, inkMapTexture);
    }

    private void ApplyInkToCanvas()
    {
        if (inkDripMaterial == null || inkMapTexture == null) return;

        inkDripMaterial.SetTexture("_InkMap", inkMapTexture);
        inkDripMaterial.SetColor("_InkColor", inkColor);
        inkDripMaterial.SetFloat("_DripThreshold", dripThreshold);

        Graphics.Blit(canvasRenderTexture, tempRenderTexture, inkDripMaterial);
        Graphics.Blit(tempRenderTexture, canvasRenderTexture);
    }

    // ─────────────────────────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────────────────────────

    private RenderTexture CreateRT(int width, int height, RenderTextureFormat format)
    {
        var desc = new RenderTextureDescriptor(width, height, format, 0)
        {
            sRGB = false
        };
        var rt = new RenderTexture(desc)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        return rt;
    }

    void OnDestroy()
    {
        ReleaseRT(tempRenderTexture);
        ReleaseRT(inkMapTexture);
        ReleaseRT(inkMapTempTexture);
    }

    private void ReleaseRT(RenderTexture rt)
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
    }
}