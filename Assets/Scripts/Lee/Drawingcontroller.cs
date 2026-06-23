using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawingController : MonoBehaviour
{
    [Header("관절 (j0 ~ j6 전부 연결)")]
    public Transform j0, j1, j2, j3, j4, j5, j6;

    [Header("참조")]
    public Transform brushTip;
    public Transform canvas;
    public CanvasPainter canvasPainter;
    public ReacherRobot reacherRobot;
    public RobotBrush robotBrush;

    [Header("타겟 이미지 (단일 테스트용)")]
    public Texture2D targetImage;

    [Header("IK 설정")]
    [Range(1, 30)] public int ikIterations = 8;
    public float arrivalThreshold = 0.2f;
    public float maxWaitTime = 0.2f;
    [Range(0.01f, 0.5f)] public float ikSpeed = 0.3f;

    [Header("획 설정")]
    [Tooltip("이미지 샘플링 간격. 작을수록 정밀하지만 느림")]
    public int samplingStep = 2;
    [Range(0f, 0.5f)] public float colorTolerance = 0.1f;
    [Range(0.5f, 1f)] public float backgroundThreshold = 0.95f;
    [Range(0.01f, 0.5f)] public float liftSpeed = 0.5f;
    [Range(0.01f, 0.5f)] public float strokeSpeed = 0.3f;
    [Range(2, 30)] public int interpolationSteps = 6;

    [Header("엣지 감지 설정")]
    [Tooltip("엣지로 판단하는 임계값 배수. 낮추면 더 많은 엣지가 감지됨")]
    [Range(0.01f, 0.6f)] public float edgeThresholdMultiplier = 0.05f;

    [Tooltip("true면 밝기(grayscale) 기반, false면 색상(RGB) 거리 기반으로 엣지 감지.\n" +
             "다색 붓터치/추상화처럼 색은 다른데 밝기가 비슷한 이미지는 false 권장")]
    public bool useGrayscaleEdge = false;

    [Header("배경 채색 설정")]
    [Tooltip("그리기 시작 전 배경색으로 캔버스를 먼저 채울지 여부")]
    public bool paintBackgroundFirst = true;

    [Tooltip("배경 채색 시 브러시 크기를 몇 배로 키울지 (큰 면을 빠르게 덮기 위함)")]
    [Range(1f, 10f)] public float backgroundBrushSizeMultiplier = 4f;

    [Tooltip("배경을 칠하는 가로 줄 수. 적을수록 빠르지만 빈틈이 생길 수 있음")]
    [Range(2, 20)] public int backgroundPaintRows = 6;

    [Tooltip("배경 채색 시 이동 속도. 너무 빠르면 점처럼 끊겨 찍힘")]
    [Range(0.01f, 0.5f)] public float backgroundStrokeSpeed = 0.06f;

    [Tooltip("배경 채색 시 보간 스텝 수. 늘리면 부드러운 긴 직선이 됨")]
    [Range(5, 60)] public int backgroundInterpolationSteps = 25;

    [Header("캔버스 월드 크기")]
    public float canvasWorldWidth = 3f;
    public float canvasWorldHeight = 1.8f;

    [Header("어린아이 스타일 (지우기/다시그리기)")]
    [Tooltip("획 하나를 그린 뒤 지우고 다시 그릴 확률 (0=안함, 1=항상)")]
    [Range(0f, 1f)] public float mistakeChance = 0f;

    [Tooltip("지우개 반경 (UV 단위)")]
    [Range(0.005f, 0.1f)] public float eraseRadius = 0.03f;

    [Tooltip("지운 뒤 '고민하는' 멈춤 시간(초)")]
    [Range(0f, 2f)] public float hesitationTime = 0.4f;

    [Tooltip("지우개로 지울 때 각 UV 포인트 사이 간격")]
    [Range(1, 10)] public int eraseStepInterval = 2;

    [Header("Python Stroke 드로잉 설정")]
    [Tooltip("stroke를 베지어 곡선으로 샘플링할 때 점 개수")]
    [Range(4, 40)] public int strokeSampleCount = 12;

    [Tooltip("stroke 두께 값(0~1)을 브러시 크기 배수로 변환할 범위 (최소,최대)")]
    public Vector2 strokeThicknessScaleRange = new Vector2(0.5f, 2.5f);

    // ─────────────────────────────────────────────────────────────
    // 내부 변수
    // ─────────────────────────────────────────────────────────────
    private Transform[] joints;
    private Vector3[] jointAxes;
    private Rigidbody[] allRigidbodies;
    private Transform[] originalParents;

    private Vector3 currentTarget;
    private bool isMoving = false;
    private bool isPainting = false;
    private Color lastDetectedBgColor = Color.white;

    private class StrokeGroup
    {
        public Color color;
        public List<List<Vector2>> paths = new List<List<Vector2>>();
    }

    void Start()
    {
        joints = new Transform[] { j6, j5, j4, j3, j2, j1 };
        jointAxes = new Vector3[]
        {
            Vector3.up, Vector3.right, Vector3.up,
            Vector3.right, Vector3.right, Vector3.up,
        };

        var rbList = new List<Rigidbody>();
        foreach (var t in new Transform[] { j0, j1, j2, j3, j4, j5, j6 })
        {
            if (t == null) continue;
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null) rbList.Add(rb);
        }
        allRigidbodies = rbList.ToArray();

        originalParents = new Transform[]
        {
            j0?.parent, j1?.parent, j2?.parent, j3?.parent,
            j4?.parent, j5?.parent, j6?.parent,
        };

        Debug.Log("[DrawingController] 초기화 완료");
    }

    void Update()
    {
        if (isMoving && brushTip != null)
            SolveIK(currentTarget);

        if (Input.GetKeyDown(KeyCode.Space) && !isPainting)
        {
            if (targetImage == null) { Debug.LogError("targetImage 없음!"); return; }
            StartCoroutine(StartDrawing(targetImage));
        }
    }

    public void StartDrawingExternal()
    {
        if (isPainting || targetImage == null) return;
        StartCoroutine(StartDrawing(targetImage));
    }

    public void StartDreamDrawing(List<Texture2D> dreamImages)
    {
        if (isPainting || dreamImages == null || dreamImages.Count == 0) return;
        StartCoroutine(StartDrawing(dreamImages[0]));
    }

    // ─────────────────────────────────────────────────────────────
    // Python stroke 데이터로 직접 그리기 (13차원 파라미터 배열)
    //   [0]x0 [1]y0 [2]cx [3]cy [4]x1 [5]y1
    //   [6]두께 [7]불투명도 [8]R [9]G [10]B [11]각도 [12]길이
    // ─────────────────────────────────────────────────────────────
    public void StartStrokeDrawing(List<float[]> strokes)
    {
        if (isPainting || strokes == null || strokes.Count == 0) return;
        StartCoroutine(StartStrokeDrawingCoroutine(strokes));
    }

    IEnumerator StartStrokeDrawingCoroutine(List<float[]> strokes)
    {
        isPainting = true;
        Debug.Log("=== Python stroke 그림 그리기 시작: " + strokes.Count + "개 획 ===");

        if (reacherRobot != null) reacherRobot.enabled = false;
        SetKinematic(true);
        yield return new WaitForSeconds(0.1f);

        BuildJointChain();
        yield return null;

        yield return StartCoroutine(MoveSmoothly(
            canvas.position + canvas.up * 0.1f, liftSpeed));

        // ── 배경을 즉시 단색으로 채우기 (로봇팔 동작 없이 즉시 적용) ──
        if (paintBackgroundFirst && canvasPainter != null)
        {
            Color bgColor = EstimateBackgroundFromStrokes(strokes);
            canvasPainter.FillCanvasColor(bgColor);
            yield return null;  // 한 프레임 대기 (적용 시점 보장)
        }

        int totalStrokes = strokes.Count;
        int strokeIndex = 0;
        foreach (var s in strokes)
        {
            strokeIndex++;
            if (strokeIndex % 10 == 0 || strokeIndex == totalStrokes)
                Debug.Log($"[Stroke 진행] {strokeIndex} / {totalStrokes}");
            yield return StartCoroutine(DrawParametricStroke(s));
        }

        isMoving = false;
        Debug.Log($"=== Python stroke 그림 그리기 완료! (총 {totalStrokes}개) ===");

        RestoreJointChain();
        SetKinematic(false);
        if (reacherRobot != null) reacherRobot.enabled = true;
        if (robotBrush != null) robotBrush.enabled = true;
        isPainting = false;
    }

    // stroke 13차원 → 2차 베지어 곡선 샘플링 → UV 경로 생성 → DrawPath 호출
    IEnumerator DrawParametricStroke(float[] s)
    {
        if (s == null || s.Length < 11) yield break;

        // Python(PIL/numpy) 이미지 Y축은 위가 0, Unity UV는 아래가 0 → Y 반전 보정
        float x0 = s[0], y0 = 1f - s[1];
        float cx = s[2], cy = 1f - s[3];
        float x1 = s[4], y1 = 1f - s[5];
        float thickness = s[6];
        float opacity = (s.Length > 7) ? Mathf.Clamp01(s[7]) : 1f;
        Color color = new Color(
            Mathf.Clamp01(s[8]),
            Mathf.Clamp01(s[9]),
            Mathf.Clamp01(s[10]),
            1f);

        // 2차 베지어 곡선을 여러 점으로 샘플링
        List<Vector2> path = new List<Vector2>(strokeSampleCount + 1);
        int samples = strokeSampleCount;
        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            float u = (1 - t) * (1 - t) * x0 + 2 * (1 - t) * t * cx + t * t * x1;
            float v = (1 - t) * (1 - t) * y0 + 2 * (1 - t) * t * cy + t * t * y1;
            path.Add(new Vector2(u, v));
        }

        // 두께(0~1)에 비례해서 브러시 크기 잠깐 조절
        float originalSize = 0.035f;
        bool hasSize = robotBrush != null && robotBrush.brushMaterial != null
                       && robotBrush.brushMaterial.HasProperty("_BrushSize");
        if (hasSize)
        {
            originalSize = robotBrush.brushMaterial.GetFloat("_BrushSize");
            float scale = Mathf.Lerp(
                strokeThicknessScaleRange.x,
                strokeThicknessScaleRange.y,
                Mathf.Clamp01(thickness));
            robotBrush.brushMaterial.SetFloat("_BrushSize", originalSize * scale);
        }

        // 불투명도(opacity) 적용 — 색상 알파에 반영
        Color strokeColor = new Color(color.r, color.g, color.b, opacity);
        yield return StartCoroutine(DrawPath(path, strokeSpeed, strokeColor));

        if (hasSize)
            robotBrush.brushMaterial.SetFloat("_BrushSize", originalSize);
    }

    // stroke들의 평균 색상으로 배경색 추정 (타겟 평균 색상과 유사하게 나옴)
    Color EstimateBackgroundFromStrokes(List<float[]> strokes)
    {
        if (strokes == null || strokes.Count == 0) return Color.white;

        float r = 0f, g = 0f, b = 0f;
        foreach (var s in strokes)
        {
            r += s[8];
            g += s[9];
            b += s[10];
        }
        int n = strokes.Count;
        Color avg = new Color(r / n, g / n, b / n, 1f);
        // 살짝만 어둡게 보정 (검정 혼합 비율을 0.4 → 0.15로 완화, 과하게 어두워지지 않도록)
        return avg * 0.85f + Color.black * 0.15f;
    }

    // ─────────────────────────────────────────────────────────────
    // 메인 드로잉 코루틴 (이미지 기반 — 기존 기능)
    // ─────────────────────────────────────────────────────────────
    IEnumerator StartDrawing(Texture2D image)
    {
        isPainting = true;
        Debug.Log("=== 그림 그리기 시작 ===");

        if (reacherRobot != null) reacherRobot.enabled = false;
        SetKinematic(true);
        yield return new WaitForSeconds(0.1f);

        BuildJointChain();
        yield return null;

        yield return StartCoroutine(MoveSmoothly(
            canvas.position + canvas.up * 0.1f, liftSpeed));

        List<StrokeGroup> groups = BuildStrokeGroups(image);
        List<List<Vector2>> sortedPaths = GetAllPathsSorted();

        // ── 배경을 먼저 큰 브러시로 빠르게 채색 ────────────────
        if (paintBackgroundFirst && lastDetectedBgColor.a > 0.1f)
            yield return StartCoroutine(PaintBackground(lastDetectedBgColor));

        yield return StartCoroutine(DrawAllStrokes(groups, sortedPaths));

        isMoving = false;
        Debug.Log("=== 그림 그리기 완료 ===");

        RestoreJointChain();
        SetKinematic(false);
        if (reacherRobot != null) reacherRobot.enabled = true;
        if (robotBrush != null) robotBrush.enabled = true;
        isPainting = false;
    }

    private List<StrokeGroup> lastBuiltGroups = new List<StrokeGroup>();

    // ─────────────────────────────────────────────────────────────
    // [최적화] 엣지 픽셀 추출 + 경로 잇기
    //
    // 변경점:
    //  1) 색상거리 계산에서 Mathf.Sqrt 제거 (비교 목적이라 제곱값이면 충분)
    //     → 이미지 전체(W×H) 픽셀에 대해 픽셀당 최대 8회 호출되던 sqrt 제거
    //  2) 경로 잇기(최근접 미사용 점 탐색)를 O(N²) 전체 선형탐색에서
    //     공간 그리드(spatial hash grid) 기반 O(N)에 가깝게 변경
    //     → 엣지 픽셀 수가 많아질수록(고해상도/낮은 threshold) 체감 효과 큼
    // ─────────────────────────────────────────────────────────────
    List<StrokeGroup> BuildStrokeGroups(Texture2D image)
    {
        lastBuiltGroups = new List<StrokeGroup>();
        int imgW = image.width;
        int imgH = image.height;
        Color[] pixels = image.GetPixels();
        Color bgColor = DetectBackgroundColor(pixels, imgW, imgH);
        lastDetectedBgColor = bgColor;

        float[,] edgeMap = new float[imgW, imgH];
        float maxEdge = 0f;

        for (int y = 1; y < imgH - 1; y++)
        {
            for (int x = 1; x < imgW - 1; x++)
            {
                Color c = pixels[y * imgW + x];
                if (IsBackground(c, bgColor)) continue;

                float g;

                if (useGrayscaleEdge)
                {
                    float tl = GetBrightness(pixels[(y - 1) * imgW + (x - 1)]);
                    float tm = GetBrightness(pixels[(y - 1) * imgW + x]);
                    float tr = GetBrightness(pixels[(y - 1) * imgW + (x + 1)]);
                    float ml = GetBrightness(pixels[y * imgW + (x - 1)]);
                    float mr = GetBrightness(pixels[y * imgW + (x + 1)]);
                    float bl2 = GetBrightness(pixels[(y + 1) * imgW + (x - 1)]);
                    float bm = GetBrightness(pixels[(y + 1) * imgW + x]);
                    float br2 = GetBrightness(pixels[(y + 1) * imgW + (x + 1)]);

                    float gx = -tl - 2 * ml - bl2 + tr + 2 * mr + br2;
                    float gy = -tl - 2 * tm - tr + bl2 + 2 * bm + br2;
                    // sqrt 제거: 임계값 비교용이므로 제곱 크기로 충분 (단조 변환)
                    g = gx * gx + gy * gy;
                }
                else
                {
                    float dTl = ColorDistanceSq(pixels[(y - 1) * imgW + (x - 1)], c);
                    float dTm = ColorDistanceSq(pixels[(y - 1) * imgW + x], c);
                    float dTr = ColorDistanceSq(pixels[(y - 1) * imgW + (x + 1)], c);
                    float dMl = ColorDistanceSq(pixels[y * imgW + (x - 1)], c);
                    float dMr = ColorDistanceSq(pixels[y * imgW + (x + 1)], c);
                    float dBl2 = ColorDistanceSq(pixels[(y + 1) * imgW + (x - 1)], c);
                    float dBm = ColorDistanceSq(pixels[(y + 1) * imgW + x], c);
                    float dBr2 = ColorDistanceSq(pixels[(y + 1) * imgW + (x + 1)], c);

                    g = (dTl + dTm + dTr + dMl + dMr + dBl2 + dBm + dBr2) / 8f;
                }

                edgeMap[x, y] = g;
                if (g > maxEdge) maxEdge = g;
            }
        }

        float edgeThreshold = maxEdge * edgeThresholdMultiplier;
        List<Vector2Int> edgePixels = new List<Vector2Int>();
        for (int y = 1; y < imgH - 1; y += samplingStep)
            for (int x = 1; x < imgW - 1; x += samplingStep)
                if (edgeMap[x, y] > edgeThreshold && !IsBackground(pixels[y * imgW + x], bgColor))
                    edgePixels.Add(new Vector2Int(x, y));

        Debug.Log("엣지 픽셀 수: " + edgePixels.Count + " (threshold=" + edgeThreshold.ToString("F4") + ", maxEdge=" + maxEdge.ToString("F4") + ")");

        // ── 공간 그리드 구축: 셀 크기 = searchRadius ─────────────
        // 이렇게 하면 "반경 내 최근접 점" 탐색 시 자기 셀 + 인접 8셀만 보면 됨
        int searchRadius = samplingStep * 3;
        int cellSize = Mathf.Max(1, searchRadius);
        var grid = new Dictionary<Vector2Int, List<int>>();
        for (int idx = 0; idx < edgePixels.Count; idx++)
        {
            Vector2Int cell = new Vector2Int(edgePixels[idx].x / cellSize, edgePixels[idx].y / cellSize);
            if (!grid.TryGetValue(cell, out var list))
            {
                list = new List<int>();
                grid[cell] = list;
            }
            list.Add(idx);
        }

        bool[] used = new bool[edgePixels.Count];
        float searchRadiusSq = (float)searchRadius * searchRadius;

        for (int i = 0; i < edgePixels.Count; i++)
        {
            if (used[i]) continue;

            List<Vector2> path = new List<Vector2>();
            Color pathColor = pixels[edgePixels[i].y * imgW + edgePixels[i].x];
            int current = i;
            used[current] = true;
            path.Add(new Vector2((float)edgePixels[current].x / imgW,
                                 (float)edgePixels[current].y / imgH));

            for (int step = 0; step < 500; step++)
            {
                int nearest = FindNearestUnusedInGrid(
                    edgePixels, grid, cellSize, used, current, pathColor, pixels, imgW, searchRadiusSq);

                if (nearest == -1) break;
                used[nearest] = true;
                current = nearest;
                path.Add(new Vector2((float)edgePixels[current].x / imgW,
                                     (float)edgePixels[current].y / imgH));
            }

            if (path.Count > 2)
                AddPathToGroup(lastBuiltGroups, pathColor, path);
        }

        Debug.Log("색상 그룹: " + lastBuiltGroups.Count + "개");

        return lastBuiltGroups;
    }

    // 현재 점 기준 3x3 인접 그리드 셀 안에서만 "색이 비슷하면서 가장 가까운 미사용 점"을 찾음
    // (예전: edgePixels 전체를 매번 선형탐색 → O(N).  이제: 인접 셀의 점들만 검사 → 거의 O(1)~O(k))
    int FindNearestUnusedInGrid(
        List<Vector2Int> points, Dictionary<Vector2Int, List<int>> grid, int cellSize,
        bool[] used, int currentIdx, Color pathColor, Color[] pixels, int imgW, float searchRadiusSq)
    {
        Vector2Int p = points[currentIdx];
        Vector2Int currentCell = new Vector2Int(p.x / cellSize, p.y / cellSize);

        int nearest = -1;
        float minDistSq = searchRadiusSq;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector2Int cell = new Vector2Int(currentCell.x + dx, currentCell.y + dy);
                if (!grid.TryGetValue(cell, out var indices)) continue;

                foreach (int j in indices)
                {
                    if (used[j]) continue;

                    Vector2Int diff = points[j] - p;
                    float distSq = diff.x * diff.x + diff.y * diff.y;
                    if (distSq >= minDistSq) continue;

                    Color jColor = pixels[points[j].y * imgW + points[j].x];
                    if (ColorSimilar(jColor, pathColor))
                    {
                        minDistSq = distSq;
                        nearest = j;
                    }
                }
            }
        }

        return nearest;
    }

    float GetBrightness(Color c) => (c.r + c.g + c.b) / 3f;

    List<List<Vector2>> GetAllPathsSorted()
    {
        List<List<Vector2>> allPaths = new List<List<Vector2>>();
        foreach (var g in lastBuiltGroups)
            allPaths.AddRange(g.paths);

        List<List<Vector2>> sorted = new List<List<Vector2>>(allPaths.Count);
        bool[] used = new bool[allPaths.Count];
        Vector2 currentPos = new Vector2(0.5f, 0.5f);

        // 참고: 경로 개수(P)는 보통 엣지 픽셀 수보다 훨씬 작아(수십~수백 단위)
        // O(P²)라도 실질적인 병목이 되는 경우는 드뭅니다.
        // 만약 P가 수천 단위로 커지면 BuildStrokeGroups와 동일한 그리드 기법을 적용하세요.
        while (sorted.Count < allPaths.Count)
        {
            float minDistSq = float.MaxValue;
            int nearest = 0;
            for (int i = 0; i < allPaths.Count; i++)
            {
                if (used[i] || allPaths[i].Count == 0) continue;
                float distSq = (currentPos - allPaths[i][0]).sqrMagnitude;
                if (distSq < minDistSq) { minDistSq = distSq; nearest = i; }
            }
            sorted.Add(allPaths[nearest]);
            currentPos = allPaths[nearest][allPaths[nearest].Count - 1];
            used[nearest] = true;
        }

        return sorted;
    }

    // ─────────────────────────────────────────────────────────────
    // 모든 획 그리기 (단일 브러시만 사용, 실수 판정 포함)
    // ─────────────────────────────────────────────────────────────
    IEnumerator DrawAllStrokes(List<StrokeGroup> groups, List<List<Vector2>> sortedPaths)
    {
        Dictionary<List<Vector2>, Color> pathColorMap = new Dictionary<List<Vector2>, Color>();
        foreach (var g in groups)
            foreach (var path in g.paths)
                pathColorMap[path] = g.color;

        foreach (var path in sortedPaths)
            yield return StartCoroutine(DrawSinglePath(path, pathColorMap));
    }

    // ─────────────────────────────────────────────────────────────
    // 단일 경로 그리기
    // ─────────────────────────────────────────────────────────────
    IEnumerator DrawSinglePath(List<Vector2> path, Dictionary<List<Vector2>, Color> pathColorMap)
    {
        if (path.Count == 0) yield break;

        Color pathColor = pathColorMap.ContainsKey(path) ? pathColorMap[path] : Color.black;

        SetBrushColor(pathColor);

        // ── 획 그리기 ──────────────────────────────────────────────
        yield return StartCoroutine(DrawPath(path, strokeSpeed, pathColor));

        // ── 실수 판정: mistakeChance 확률로 지우고 다시 그리기 ──────
        if (Random.value < mistakeChance)
        {
            Debug.Log("[Childlike] 실수! 지우고 다시 그리는 중...");

            yield return StartCoroutine(EraseStroke(path));

            if (hesitationTime > 0f)
                yield return new WaitForSeconds(hesitationTime);

            Debug.Log("[Childlike] 다시 그리기...");
            yield return StartCoroutine(DrawPath(path, strokeSpeed, pathColor));
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 배경 채색 — 브러시 사이즈를 키워서 큰 면을 빠르게 덮음
    // ─────────────────────────────────────────────────────────────
    IEnumerator PaintBackground(Color bgColor)
    {
        Debug.Log("[배경] 채색 시작: " + bgColor);

        if (robotBrush == null || robotBrush.brushMaterial == null) yield break;

        SetBrushColor(bgColor);

        // 원래 사이즈 저장 후 임시로 키우기
        float originalSize = 0.035f;
        bool hasSize = robotBrush.brushMaterial.HasProperty("_BrushSize");
        if (hasSize)
        {
            originalSize = robotBrush.brushMaterial.GetFloat("_BrushSize");
            robotBrush.brushMaterial.SetFloat("_BrushSize", originalSize * backgroundBrushSizeMultiplier);
        }

        // ── 지그재그로 가로 줄을 그어 캔버스 전체 덮기 ───────────
        for (int row = 0; row < backgroundPaintRows; row++)
        {
            float y = backgroundPaintRows <= 1 ? 0.5f : (float)row / (backgroundPaintRows - 1);
            List<Vector2> rowPath = new List<Vector2>();

            if (row % 2 == 0)
            {
                rowPath.Add(new Vector2(0f, y));
                rowPath.Add(new Vector2(1f, y));
            }
            else
            {
                rowPath.Add(new Vector2(1f, y));
                rowPath.Add(new Vector2(0f, y));
            }

            yield return StartCoroutine(DrawPath(rowPath, backgroundStrokeSpeed, bgColor, backgroundInterpolationSteps));
        }

        // ── 브러시 사이즈 원복 ────────────────────────────────────
        if (hasSize)
            robotBrush.brushMaterial.SetFloat("_BrushSize", originalSize);

        Debug.Log("[배경] 채색 완료");
    }

    // ─────────────────────────────────────────────────────────────
    // 지우개 코루틴
    // ─────────────────────────────────────────────────────────────
    IEnumerator EraseStroke(List<Vector2> path)
    {
        if (canvasPainter == null) yield break;
        if (robotBrush != null) robotBrush.enabled = false;

        yield return StartCoroutine(MoveSmoothly(
            UVToWorld(path[0]) - canvas.up * 0.2f, liftSpeed));

        yield return StartCoroutine(MoveSmoothly(UVToWorld(path[0]), liftSpeed));

        for (int i = 0; i < path.Count; i += eraseStepInterval)
        {
            Vector2 uv = path[i];
            canvasPainter.EraseInkAtUV(uv, eraseRadius);

            currentTarget = UVToWorld(uv);
            isMoving = true;

            float elapsed = 0f;
            while (elapsed < maxWaitTime)
            {
                if (Vector3.Distance(brushTip.position, currentTarget) < arrivalThreshold) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (path.Count > 0)
            canvasPainter.EraseInkAtUV(path[path.Count - 1], eraseRadius);

        isMoving = false;

        yield return StartCoroutine(MoveSmoothly(
            UVToWorld(path[0]) - canvas.up * 0.2f, liftSpeed));

        Debug.Log("[Childlike] 지우기 완료");
    }

    // ─────────────────────────────────────────────────────────────
    // 브러시 색상 설정 (단일 브러시 + canvasPainter inkColor)
    // ─────────────────────────────────────────────────────────────
    private void SetBrushColor(Color color)
    {
        if (robotBrush != null)
            robotBrush.SetColor(color);

        if (canvasPainter != null)
            canvasPainter.inkColor = color;
    }

    // ─────────────────────────────────────────────────────────────
    // 획 그리기
    // ─────────────────────────────────────────────────────────────
    IEnumerator DrawPath(List<Vector2> path, float speed, Color color)
    {
        yield return StartCoroutine(DrawPath(path, speed, color, interpolationSteps));
    }

    // 보간 스텝 수를 직접 지정할 수 있는 오버로드 (배경 채색처럼 긴 직선에 사용)
    IEnumerator DrawPath(List<Vector2> path, float speed, Color color, int customInterpolationSteps)
    {
        if (path.Count == 0) yield break;

        SetBrushColor(color);

        if (robotBrush != null) robotBrush.enabled = false;
        yield return StartCoroutine(MoveSmoothly(
            UVToWorld(path[0]) - canvas.up * 0.2f, liftSpeed));

        yield return StartCoroutine(MoveSmoothly(UVToWorld(path[0]), speed));

        if (robotBrush != null) robotBrush.enabled = true;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = UVToWorld(path[i - 1]);
            Vector3 to = UVToWorld(path[i]);

            for (int s = 1; s <= customInterpolationSteps; s++)
            {
                float t = (float)s / customInterpolationSteps;
                float smooth = t * t * (3f - 2f * t);
                currentTarget = Vector3.Lerp(from, to, smooth);
                isMoving = true;

                float elapsed = 0f;
                while (elapsed < maxWaitTime)
                {
                    if (Vector3.Distance(brushTip.position, currentTarget) < arrivalThreshold) break;
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        if (robotBrush != null) robotBrush.enabled = false;
        isMoving = false;
    }

    // ─────────────────────────────────────────────────────────────
    // 배경/색상 유틸리티
    // ─────────────────────────────────────────────────────────────
    Color DetectBackgroundColor(Color[] pixels, int imgW, int imgH)
    {
        if (pixels[0].a < 0.1f || pixels[imgW - 1].a < 0.1f)
        {
            Debug.Log("투명 배경 감지");
            return Color.clear;
        }

        Color tl = pixels[0];
        Color tr = pixels[imgW - 1];
        Color bl = pixels[(imgH - 1) * imgW];
        Color br = pixels[(imgH - 1) * imgW + imgW - 1];

        float maxDiff = Mathf.Max(
            ColorDistance(tl, tr),
            Mathf.Max(ColorDistance(tl, bl), ColorDistance(tl, br)));

        if (maxDiff < 0.25f)
        {
            Color avg = new Color(
                (tl.r + tr.r + bl.r + br.r) / 4f,
                (tl.g + tr.g + bl.g + br.g) / 4f,
                (tl.b + tr.b + bl.b + br.b) / 4f, 1f);
            Debug.Log("단색 배경 감지: " + avg);
            return avg;
        }

        Debug.Log("배경 없음 -> 전체 그리기");
        return Color.clear;
    }

    float ColorDistance(Color a, Color b)
    {
        return Mathf.Sqrt(ColorDistanceSq(a, b));
    }

    // [최적화] sqrt 없는 제곱 거리. 임계값 비교용으로는 ColorDistance와 동일하게 쓸 수 있음
    // (threshold도 제곱해서 비교하면 결과가 같음: a < b  ⇔  a² < b² , a,b ≥ 0)
    float ColorDistanceSq(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return dr * dr + dg * dg + db * db;
    }

    bool IsBackground(Color pixel, Color bgColor)
    {
        if (pixel.a < 0.1f) return true;
        if (bgColor.a < 0.1f)
            return (pixel.r + pixel.g + pixel.b) / 3f > backgroundThreshold;
        float diffSq = ColorDistanceSq(pixel, bgColor);
        float bgBrightness = (bgColor.r + bgColor.g + bgColor.b) / 3f;
        // 원래 threshold(0.25 / 0.15)를 제곱한 값으로 비교 (sqrt 제거)
        float thresholdSq = bgBrightness < 0.2f ? 0.0625f : 0.0225f;
        return diffSq < thresholdSq;
    }

    void AddPathToGroup(List<StrokeGroup> groups, Color color, List<Vector2> path)
    {
        StrokeGroup target = null;
        foreach (var g in groups)
            if (ColorSimilar(g.color, color)) { target = g; break; }
        if (target == null) { target = new StrokeGroup { color = color }; groups.Add(target); }
        target.paths.Add(new List<Vector2>(path));
    }

    // ─────────────────────────────────────────────────────────────
    // IK / 관절 유틸리티
    // ─────────────────────────────────────────────────────────────
    void BuildJointChain()
    {
        if (j1 != null) j1.SetParent(j0, true);
        if (j2 != null) j2.SetParent(j1, true);
        if (j3 != null) j3.SetParent(j2, true);
        if (j4 != null) j4.SetParent(j3, true);
        if (j5 != null) j5.SetParent(j4, true);
        if (j6 != null) j6.SetParent(j5, true);
    }

    void RestoreJointChain()
    {
        Transform[] joints7 = { j0, j1, j2, j3, j4, j5, j6 };
        for (int i = 0; i < joints7.Length; i++)
            if (joints7[i] != null)
                joints7[i].SetParent(originalParents[i], true);
    }

    void SolveIK(Vector3 target)
    {
        for (int iter = 0; iter < ikIterations; iter++)
        {
            if (Vector3.Distance(brushTip.position, target) < arrivalThreshold * 0.5f) break;
            for (int i = 0; i < joints.Length; i++)
            {
                Transform joint = joints[i];
                if (joint == null) continue;
                Vector3 axis = jointAxes[i];
                Vector3 toTip = brushTip.position - joint.position;
                Vector3 toTarget = target - joint.position;
                Vector3 projTip = Vector3.ProjectOnPlane(toTip, axis);
                Vector3 projTarget = Vector3.ProjectOnPlane(toTarget, axis);
                if (projTip.magnitude < 0.001f || projTarget.magnitude < 0.001f) continue;
                float angle = Vector3.SignedAngle(projTip, projTarget, axis);
                float step = Mathf.Clamp(angle, -45f, 45f) * ikSpeed;
                joint.Rotate(axis, step, Space.World);
            }
        }
    }

    IEnumerator MoveSmoothly(Vector3 target, float speed)
    {
        isMoving = true;
        float elapsed = 0f;
        float timeout = 3f;
        float prevIkSpeed = ikSpeed;

        while (elapsed < timeout)
        {
            float dist = Vector3.Distance(brushTip.position, target);
            ikSpeed = Mathf.Lerp(speed * 0.3f, speed, Mathf.Clamp01(dist));
            currentTarget = target;
            if (dist < arrivalThreshold) break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        ikSpeed = prevIkSpeed;
        isMoving = false;
    }

    Vector3 UVToWorld(Vector2 uv)
    {
        float localX = (uv.x - 0.5f) * canvasWorldWidth;
        float localZ = (uv.y - 0.5f) * canvasWorldHeight;
        return canvas.transform.position
             + canvas.transform.right * localX
             + canvas.transform.forward * localZ
             + canvas.transform.up * 0.1f;
    }

    void SetKinematic(bool value)
    {
        foreach (var rb in allRigidbodies)
            if (rb != null) rb.isKinematic = value;
    }

    bool ColorSimilar(Color a, Color b)
        => Mathf.Abs(a.r - b.r) < colorTolerance
        && Mathf.Abs(a.g - b.g) < colorTolerance
        && Mathf.Abs(a.b - b.b) < colorTolerance;
}