using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageToPainter : MonoBehaviour
{
    [Header("참조")]
    public RobotIKSolver ikSolver;
    public Transform brushTip;
    public RobotBrush robotBrush;
    public Transform canvas;

    [Header("타겟 이미지")]
    public Texture2D targetImage;

    [Header("이동 설정")]
    public float arrivalThreshold = 0.05f;
    public float maxWaitTime = 3f;

    [Header("이미지 설정")]
    public int samplingStep = 4;
    [Range(0f, 0.3f)] public float colorTolerance = 0.1f;
    [Range(0.5f, 1f)] public float backgroundThreshold = 0.95f;

    [Header("캔버스 월드 크기")]
    public float canvasWorldWidth = 4f;
    public float canvasWorldHeight = 4f;

    private bool isPainting = false;
    private List<StrokeGroup> strokeGroups = new List<StrokeGroup>();

    private class StrokeGroup
    {
        public Color color;
        public List<List<Vector2>> paths = new List<List<Vector2>>();
    }

    void Start()
    {
        if (ikSolver == null)
        {
            Debug.LogError("[ImageToPainter] RobotIKSolver가 연결되지 않았습니다!");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isPainting)
        {
            if (targetImage == null)
            {
                Debug.LogError("[ImageToPainter] targetImage가 할당되지 않았습니다!");
                return;
            }
            if (ikSolver == null)
            {
                Debug.LogError("[ImageToPainter] RobotIKSolver가 연결되지 않았습니다!");
                return;
            }
            StartCoroutine(PaintImage());
        }
    }

    IEnumerator PaintImage()
    {
        isPainting = true;

        Debug.Log("[ImageToPainter] 이미지 분석 시작...");
        BuildStrokeGroups();
        Debug.Log($"[ImageToPainter] 색상 그룹 {strokeGroups.Count}개 생성 완료");

        foreach (var group in strokeGroups)
        {
            // 붓 색상 설정 (brushes[0] = BrushMaterial)
            if (robotBrush != null)
                robotBrush.SetColor(group.color);

            yield return new WaitForSeconds(0.1f);

            foreach (var path in group.paths)
            {
                if (path.Count == 0) continue;

                yield return StartCoroutine(MoveTo(path[0], liftBrush: true));

                foreach (var uv in path)
                    yield return StartCoroutine(MoveTo(uv, liftBrush: false));
            }
        }

        Debug.Log("[ImageToPainter] 그리기 완료!");
        isPainting = false;
    }

    IEnumerator MoveTo(Vector2 uv, bool liftBrush)
    {
        Vector3 worldPos = UVToWorld(uv);

        if (liftBrush)
            worldPos += canvas.transform.forward * 0.15f;

        ikSolver.SetTarget(worldPos);

        float elapsed = 0f;
        while (elapsed < maxWaitTime)
        {
            float dist = Vector3.Distance(brushTip.position, worldPos);
            if (dist < arrivalThreshold) break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void BuildStrokeGroups()
    {
        strokeGroups.Clear();
        int imgW = targetImage.width;
        int imgH = targetImage.height;

        for (int y = 0; y < imgH; y += samplingStep)
        {
            List<Vector2> currentPath = null;
            Color currentColor = Color.clear;

            for (int x = 0; x < imgW; x += samplingStep)
            {
                Color pixel = targetImage.GetPixel(x, y);

                if (IsBackground(pixel))
                {
                    if (currentPath != null && currentPath.Count > 1)
                        AddPathToGroup(currentColor, currentPath);
                    currentPath = null;
                    continue;
                }

                Vector2 uv = new Vector2((float)x / imgW, (float)y / imgH);

                if (currentPath == null)
                {
                    currentPath = new List<Vector2> { uv };
                    currentColor = pixel;
                }
                else if (ColorSimilar(pixel, currentColor))
                {
                    currentPath.Add(uv);
                }
                else
                {
                    if (currentPath.Count > 1)
                        AddPathToGroup(currentColor, currentPath);
                    currentPath = new List<Vector2> { uv };
                    currentColor = pixel;
                }
            }

            if (currentPath != null && currentPath.Count > 1)
                AddPathToGroup(currentColor, currentPath);
        }
    }

    void AddPathToGroup(Color color, List<Vector2> path)
    {
        StrokeGroup target = null;
        foreach (var g in strokeGroups)
        {
            if (ColorSimilar(g.color, color))
            {
                target = g;
                break;
            }
        }

        if (target == null)
        {
            target = new StrokeGroup { color = color };
            strokeGroups.Add(target);
        }

        target.paths.Add(new List<Vector2>(path));
    }

    Vector3 UVToWorld(Vector2 uv)
    {
        float localX = (uv.x - 0.5f) * canvasWorldWidth;
        float localY = (uv.y - 0.5f) * canvasWorldHeight;
        return canvas.transform.position
             + canvas.transform.up * localX
             + canvas.transform.right * localY;
    }

    bool IsBackground(Color c)
    {
        return (c.r + c.g + c.b) / 3f > backgroundThreshold;
    }

    bool ColorSimilar(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < colorTolerance &&
               Mathf.Abs(a.g - b.g) < colorTolerance &&
               Mathf.Abs(a.b - b.b) < colorTolerance;
    }

    void OnDrawGizmosSelected()
    {
        if (isPainting && ikSolver != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(brushTip.position, 0.05f);
        }
    }
}