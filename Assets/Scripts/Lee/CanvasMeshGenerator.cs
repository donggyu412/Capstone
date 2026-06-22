using UnityEngine;

// 캔버스 메시를 절차적으로 생성하는 스크립트
// N x N 버텍스 Plane을 만들고 다중 Perlin Noise로 종이 질감을 구현한다
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CanvasMeshGenerator : MonoBehaviour
{
    [Header("Canvas size setting")]
    public int resolution = 100;       // 가로/세로 버텍스 수 (클수록 디테일해짐)
    public float canvasSize = 10.0f;   // 캔버스 실제 크기 (Unity 단위)
    public float canvasDepth = 0.3f;

    [Header("Noise setting")]
    public float noiseScale = 5f;      // Perlin Noise 스케일 (클수록 패턴이 촘촘해짐)
    public float noiseHeight = 0.005f; // 울퉁불퉁 높이 (작을수록 종이처럼 평평해짐)

    // 내부에서 사용할 메시 데이터
    // DirectX의 Vertex Buffer와 동일한 개념
    private Mesh mesh;
    private Vector3[] vertices;

    // 각 버텍스의 높이값 배열
    // 붓 팀원이 GetHeightAtUV()로 접근해서 잉크 농도 계산에 활용
    private float[] heights;

    // 높이값을 텍스처로 변환한 HeightMap
    // 나중에 커스텀 셰이더에서 물감 퍼짐 등에 활용 예정
    private Texture2D heightMap;

    // Inspector에서 연결할 Render Texture
    public RenderTexture canvasRenderTexture;

    void Start()
    {
        GenerateMesh();
        InitializeRenderTexture();
    }

    // Inspector에서 값이 바뀔 때마다 자동 호출
    // Update()와 달리 필요할 때만 호출되므로 성능 낭비 없음
    void OnValidate()
    {
        if (resolution < 2) resolution = 2;
        Debug.Log("OnValidate 호출됨!");
        GenerateMesh();
    }

    // Render Texture를 아이보리색(종이색)으로 초기화
    // DirectX의 ClearRenderTargetView와 동일한 개념
    void InitializeRenderTexture()
    {
        if (canvasRenderTexture == null) return;

        // 현재 렌더 타겟을 canvasRenderTexture로 변경
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = canvasRenderTexture;

        // 아이보리색으로 초기화
        GL.Clear(true, true, new Color(1f, 0.96f, 0.86f));

        // 렌더 타겟 복구
        RenderTexture.active = prev;
    }

    void GenerateMesh()
    {
        mesh = new Mesh();
        mesh.name = "CanvasMesh";

        // 앞면 버텍스 계산
        vertices = new Vector3[resolution * resolution];
        heights = new float[resolution * resolution];
        Vector2[] frontUVs = new Vector2[resolution * resolution];

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;

                float xNorm = (float)x / (resolution - 1);
                float zNorm = (float)z / (resolution - 1);

                float xPos = (xNorm - 0.5f) * canvasSize;
                float zPos = (zNorm - 0.5f) * canvasSize;

                // 4개 레이어 노이즈로 종이 질감 구현
                float largeNoise = Mathf.PerlinNoise(
                    xNorm * noiseScale,
                    zNorm * noiseScale);

                float mediumNoise = Mathf.PerlinNoise(
                    xNorm * noiseScale * 2f + 50f,
                    zNorm * noiseScale * 2f + 50f);

                float fineNoise = Mathf.PerlinNoise(
                    xNorm * noiseScale * 8f + 200f,
                    zNorm * noiseScale * 8f + 200f);

                float fiberNoise = Mathf.PerlinNoise(
                    xNorm * noiseScale * 20f,
                    0.5f);

                float height =
                    largeNoise * 0.50f +
                    mediumNoise * 0.30f +
                    fineNoise * 0.15f +
                    fiberNoise * 0.05f;

                float yPos = height * noiseHeight;

                vertices[index] = new Vector3(xPos, yPos, zPos);
                heights[index] = height;
                frontUVs[index] = new Vector2(xNorm, zNorm);
            }
        }

        // 앞면 인덱스 버퍼
        int frontTriCount = (resolution - 1) * (resolution - 1) * 6;
        int[] frontTris = new int[frontTriCount];
        int triIndex = 0;

        for (int z = 0; z < resolution - 1; z++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int bottomLeft = z * resolution + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = (z + 1) * resolution + x;
                int topRight = topLeft + 1;

                frontTris[triIndex++] = bottomLeft;
                frontTris[triIndex++] = topLeft;
                frontTris[triIndex++] = bottomRight;

                frontTris[triIndex++] = bottomRight;
                frontTris[triIndex++] = topLeft;
                frontTris[triIndex++] = topRight;
            }
        }

        // ─────────────────────────────────────────
        // 옆면 + 뒷면 버텍스 생성
        // 캔버스 액자의 두께를 표현하는 부분
        // ─────────────────────────────────────────

        // half = 캔버스 절반 크기
        float half = canvasSize * 0.5f;

        // 옆면 버텍스 (4방향 각 4개 = 16개)
        // 각 면마다 앞쪽 상단/하단, 뒤쪽 상단/하단 4개 버텍스 필요
        // DirectX로 비유하면 각 면이 독립된 쿼드(사각형) 2개 삼각형
        Vector3[] sideVerts = new Vector3[]
        {
        // 앞면 (Z- 방향) -- 4개
        new Vector3(-half, 0,      -half), // 0 앞 왼쪽 아래
        new Vector3( half, 0,      -half), // 1 앞 오른쪽 아래
        new Vector3(-half, -canvasDepth, -half), // 2 뒤 왼쪽 아래
        new Vector3( half, -canvasDepth, -half), // 3 뒤 오른쪽 아래

        // 뒷면 (Z+ 방향) -- 4개
        new Vector3( half, 0,       half), // 4 앞 오른쪽 위
        new Vector3(-half, 0,       half), // 5 앞 왼쪽 위
        new Vector3( half, -canvasDepth,  half), // 6 뒤 오른쪽 위
        new Vector3(-half, -canvasDepth,  half), // 7 뒤 왼쪽 위

        // 왼면 (X- 방향) -- 4개
        new Vector3(-half, 0,       half), // 8 앞 왼쪽 위
        new Vector3(-half, 0,      -half), // 9 앞 왼쪽 아래
        new Vector3(-half, -canvasDepth,  half), // 10 뒤 왼쪽 위
        new Vector3(-half, -canvasDepth, -half), // 11 뒤 왼쪽 아래

        // 오른면 (X+ 방향) -- 4개
        new Vector3( half, 0,      -half), // 12 앞 오른쪽 아래
        new Vector3( half, 0,       half), // 13 앞 오른쪽 위
        new Vector3( half, -canvasDepth, -half), // 14 뒤 오른쪽 아래
        new Vector3( half, -canvasDepth,  half), // 15 뒤 오른쪽 위
        };

        // 뒷면 버텍스 (4개)
        Vector3[] backVerts = new Vector3[]
        {
        new Vector3(-half, -canvasDepth, -half), // 0
        new Vector3( half, -canvasDepth, -half), // 1
        new Vector3(-half, -canvasDepth,  half), // 2
        new Vector3( half, -canvasDepth,  half), // 3
        };

        // 옆면 인덱스 버퍼 (4방향 각 2삼각형 = 24개)
        int[] sideTris = new int[]
        {
    // 앞면 (Z-)
    0, 1, 2,  1, 3, 2,
    // 뒷면 (Z+)
    4, 5, 6,  5, 7, 6,
    // 왼면 (X-)
    8, 9, 10,  9, 11, 10,
    // 오른면 (X+)
    12, 13, 14,  13, 15, 14,
        };

        // 뒷면 인덱스 버퍼
        int[] backTris = new int[]
        {
    0, 1, 2,  1, 3, 2,
        };

        // ─────────────────────────────────────────
        // 전체 메시 합치기
        // 앞면 + 옆면 + 뒷면을 하나의 메시로 병합
        // ─────────────────────────────────────────

        int frontVertCount = vertices.Length;
        int sideVertCount = sideVerts.Length;
        int backVertCount = backVerts.Length;
        int totalVertCount = frontVertCount + sideVertCount + backVertCount;

        // 전체 버텍스 배열 합치기
        Vector3[] allVerts = new Vector3[totalVertCount];
        Vector2[] allUVs = new Vector2[totalVertCount];

        // 앞면 버텍스 복사
        for (int i = 0; i < frontVertCount; i++)
        {
            allVerts[i] = vertices[i];
            allUVs[i] = frontUVs[i];
        }

        // 옆면 버텍스 복사
        for (int i = 0; i < sideVertCount; i++)
        {
            allVerts[frontVertCount + i] = sideVerts[i];
            allUVs[frontVertCount + i] = Vector2.zero; // 옆면은 UV 불필요
        }

        // 뒷면 버텍스 복사
        for (int i = 0; i < backVertCount; i++)
        {
            allVerts[frontVertCount + sideVertCount + i] = backVerts[i];
            allUVs[frontVertCount + sideVertCount + i] = Vector2.zero;
        }

        // 옆면 인덱스에 오프셋 적용
        int[] sideTrisFinal = new int[sideTris.Length];
        for (int i = 0; i < sideTris.Length; i++)
            sideTrisFinal[i] = sideTris[i] + frontVertCount;

        // 뒷면 인덱스에 오프셋 적용
        int[] backTrisFinal = new int[backTris.Length];
        for (int i = 0; i < backTris.Length; i++)
            backTrisFinal[i] = backTris[i] + frontVertCount + sideVertCount;

        // SubMesh로 분리 (앞면 / 옆면 / 뒷면 각각 다른 Material 적용 가능)
        mesh.subMeshCount = 3;
        mesh.SetVertices(allVerts);
        mesh.SetUVs(0, allUVs);
        mesh.SetTriangles(frontTris, 0); // SubMesh 0 = 앞면 (CanvasMaterial)
        mesh.SetTriangles(sideTrisFinal, 1); // SubMesh 1 = 옆면 (나무 느낌)
        mesh.SetTriangles(backTrisFinal, 2); // SubMesh 2 = 뒷면 (나무 느낌)

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        GetComponent<MeshFilter>().mesh = mesh;

        MeshCollider col = GetComponent<MeshCollider>();
        if (col != null)
            col.sharedMesh = mesh;

        Debug.Log("메시 생성 완료! 버텍스 수: " + mesh.vertexCount);
        GenerateHeightMap();
    }

    // heights 배열을 Texture2D로 변환
    // 나중에 커스텀 셰이더에서 _HeightMap으로 활용 예정
    void GenerateHeightMap()
    {
        heightMap = new Texture2D(resolution, resolution, TextureFormat.RFloat, false);

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;
                float h = heights[index];

                // 높이값을 흑백 픽셀로 저장 (밝을수록 높음)
                heightMap.SetPixel(x, z, new Color(h, h, h));
            }
        }

        heightMap.Apply();

        // 머티리얼에 HeightMap 전달
        // 현재는 URP/Lit 셰이더라 효과 없음
        // 나중에 커스텀 셰이더 만들면 활성화됨
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && meshRenderer.sharedMaterial != null)
        {
            meshRenderer.sharedMaterial.SetTexture("_HeightMap", heightMap);
        }
    }

    // UV 좌표 기준으로 해당 위치의 높이값 반환
    // 붓 팀원이 잉크 농도 계산 시 호출하는 공개 API
    public float GetHeightAtUV(Vector2 uv)
    {
        int x = Mathf.Clamp((int)(uv.x * (resolution - 1)), 0, resolution - 1);
        int z = Mathf.Clamp((int)(uv.y * (resolution - 1)), 0, resolution - 1);

        int index = z * resolution + x;

        return heights[index];
    }
}