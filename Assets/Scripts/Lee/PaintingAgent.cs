using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(CanvasPainter))]
[RequireComponent(typeof(CanvasMeshGenerator))]

public class PaintingAgent : Agent
{
    private CanvasPainter canvasPainter;
    private CanvasMeshGenerator canvasMeshGenerator;
    private Vector2 strokeStart;

    [Header("��ǥ �̹���")]
    public Texture2D targetImage;

    [Header("�� ����")]
    public Material brushMaterial;
    public float maxBrushMoveSpeed = 0.05f;

    //ĵ������ �ٿ� �����ϸ��� �ػ�
    //���ػ� �״�� �Է��ϸ� ���������� �ʹ� ���� ���� �Ұ���
    [Header("Observation ����")]
    public int observationResolution = 32;

    //ȹ�� �׸������� ���� ���� ���Ƽ�� ��
    [Header("���� ����")]
    public float strokePenaltyWeight = 0.001f;

    //���� ���� ����

    //0,0�� ���ϴ� 1,1�� ����
    private Vector2 brushUV;

    //���� ������ ĵ����-��ǥ �̹��� ���̰�, �� ���ܸ��� �������� �������°��� ���ؼ� ������ ��
    private float prevDistance;

    //���� ���Ǽҵ忡�� ����� ȹ ��
    private int strokeCount;

    //ĵ���� ���¸� 32*32�� �ٿ������ �ȼ� �迭
    //ML-Agent�� ������ Observation ������
    private Color[] canvasPixels;
    private Color[] targetPixels;

    private RenderTexture observationRT;

    //����Ƽ �ʱ�ȭ
    public override void Initialize()
    {
        Debug.Log("������Ʈ Initialize ȣ��");
        canvasPainter = GetComponent<CanvasPainter>();
        canvasMeshGenerator = GetComponent<CanvasMeshGenerator>();

        //�ٿ� �����ϸ��� RenderTexture����
        //�� ���ܸ��� ���� ����� �޸� �����̹Ƿ� ���⼭ 1ȸ�� ����
        observationRT = new RenderTexture(
            observationResolution,
            observationResolution,
            0,
            RenderTextureFormat.ARGB32
            );
        observationRT.Create();

        //��ǥ �̹����� 32*32�� �̸� �ٿ���ϸ� �ؼ� ĳ��
        //�� ���ܸ��� ����ϸ� �����̹Ƿ� ���⼭ 1ȸ�� ó��
        if (targetImage != null)
        {
            targetPixels = DownscaleTexture(targetImage);
        }
        else
        {
            Debug.LogWarning("PaintingAgent: Ÿ���̹��� ���� �ȵ�");
        }
    }

    //���Ǽҵ� ���� �� ȣ��
    //ĵ���� �ʱ�ȭ + �� ��ġ ����
    public override void OnEpisodeBegin()
    {
        Debug.Log("������Ʈ OnEpisodeBegin ȣ��");

        canvasPainter.ClearCanvas();
        //�� ���� ��ġ ����
        brushUV = new Vector2(
    Random.Range(0.1f, 0.9f),
    Random.Range(0.1f, 0.9f)
);
        strokeCount = 0;

        // canvasRenderTexture�� �غ���� ���� ���
        if (canvasPainter.GetCanvasTexture() != null)
        {
            canvasPixels = GetCanvasPixels();
            prevDistance = CalcL2Distance(canvasPixels, targetPixels);
        }
        else
        {
            // ���� �غ� �ȵ����� �ִ밪���� �ʱ�ȭ
            prevDistance = float.MaxValue;
            Debug.LogWarning("ĵ���� �ؽ�ó ���� �غ� �ȵ�, prevDistance �ִ밪���� �ʱ�ȭ");
        }
    }

    //������Ʈ�� ȯ���� �����ϴ� �Լ�, ���⼭ ���� ������ �Ű���� �Է��� ��
    public override void CollectObservations(VectorSensor sensor)
    {
        Debug.Log("CollectObservations ȣ���!");

        // canvasPixels�� null�̸� 0���� ä���
        // null ���¿��� return�ϸ� observation ���� 6146�� �� �¾Ƽ� ML-Agents�� ����
        if (canvasPixels == null)
            canvasPixels = new Color[observationResolution * observationResolution];

        if (targetPixels == null)
            targetPixels = new Color[observationResolution * observationResolution];

        foreach (Color pixel in canvasPixels)
        {
            sensor.AddObservation(pixel.r);
            sensor.AddObservation(pixel.g);
            sensor.AddObservation(pixel.b);
        }

        foreach (Color pixel in targetPixels)
        {
            sensor.AddObservation(pixel.r);
            sensor.AddObservation(pixel.g);
            sensor.AddObservation(pixel.b);
        }

        sensor.AddObservation(brushUV.x);
        sensor.AddObservation(brushUV.y);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // ���� ���: �� �׼� = ȹ �ϳ� �ϼ�
        // ������, ����, ���⸦ �� ���� ���
        float startX = Mathf.Clamp01(Sigmoid(actions.ContinuousActions[0]));
        float startY = Mathf.Clamp01(Sigmoid(actions.ContinuousActions[1]));
        float endX = Mathf.Clamp01(Sigmoid(actions.ContinuousActions[2]));
        float endY = Mathf.Clamp01(Sigmoid(actions.ContinuousActions[3]));
        float width = Mathf.Clamp(Sigmoid(actions.ContinuousActions[4]) * 0.1f, 0.01f, 0.1f);

        if (brushMaterial != null)
        {
            brushMaterial.SetColor("_BrushColor", new Color(0f, 0f, 0f, 1f));
            brushMaterial.SetVector("_StrokeStart", new Vector4(startX, startY, 0, 0));
            brushMaterial.SetVector("_StrokeEnd", new Vector4(endX, endY, 0, 0));
            brushMaterial.SetFloat("_StrokeWidth", width);

            float paperHeight = canvasMeshGenerator.GetHeightAtUV(new Vector2(startX, startY));
            brushMaterial.SetFloat("_PaperHeight", paperHeight);

            canvasPainter.Paint(new Vector2(startX, startY), brushMaterial, 1.0f);
            strokeCount++;
        }

        canvasPixels = GetCanvasPixels();
        float currentDistance = CalcL2Distance(canvasPixels, targetPixels);

        float reward = (prevDistance - currentDistance) + 0.001f;
        AddReward(reward);

        prevDistance = currentDistance;

        if (currentDistance < 0.001f)
        {
            SetReward(1.0f);
            EndEpisode();
        }
    }

    //���� ����
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuous = actionsOut.ContinuousActions;

        // ���� ȹ �׽�Ʈ
        continuous[0] = Random.Range(-1f, 1f); // ���� x
        continuous[1] = Random.Range(-1f, 1f); // ���� y
        continuous[2] = Random.Range(-1f, 1f); // �� x
        continuous[3] = Random.Range(-1f, 1f); // �� y
        continuous[4] = Random.Range(-1f, 1f); // ����
    }

    // ����������������������������������������������������������������������������������
    // ���� ĵ������ 32x32�� �ٿ���� �� �ȼ� �迭 ��ȯ
    // ����������������������������������������������������������������������������������
    private Color[] GetCanvasPixels()
    {
        RenderTexture canvasRT = canvasPainter.GetCanvasTexture();
        if (canvasRT == null) return new Color[observationResolution * observationResolution];

        // ���� ĵ����  32x32 observationRT�� �ٿ����
        // DirectX�� StretchRect�� ������ ����
        Graphics.Blit(canvasRT, observationRT);

        // GPU  CPU�� �ȼ� ������ �б�
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = observationRT;

        Texture2D tempTex = new Texture2D(
            observationResolution,
            observationResolution,
            TextureFormat.RGB24,
            false
        );
        tempTex.ReadPixels(new Rect(0, 0, observationResolution, observationResolution), 0, 0);
        tempTex.Apply();

        RenderTexture.active = prev;

        Color[] pixels = tempTex.GetPixels();
        Destroy(tempTex); // �޸� ���� ����

        return pixels;
    }

    // Texture2D�� 32x32�� �ٿ����
    // targetImage ��ó���� ��� (Initialize���� 1ȸ�� ȣ��)
    private Color[] DownscaleTexture(Texture2D source)
    {
        RenderTexture rt = new RenderTexture(observationResolution, observationResolution, 0);
        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(
            observationResolution,
            observationResolution,
            TextureFormat.RGB24,
            false
        );
        result.ReadPixels(new Rect(0, 0, observationResolution, observationResolution), 0, 0);
        result.Apply();

        RenderTexture.active = prev;
        rt.Release();

        return result.GetPixels();
    }

    // �� �ȼ� �迭�� L2 �Ÿ� ���
    private float CalcL2Distance(Color[] a, Color[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return float.MaxValue;

        float sum = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            float dr = a[i].r - b[i].r;
            float dg = a[i].g - b[i].g;
            float db = a[i].b - b[i].b;
            sum += dr * dr + dg * dg + db * db;
        }

        // �ȼ� ���� ������ ����ȭ
        return sum / a.Length;
    }

    // Sigmoid �Լ� (  0~1)
    private float Sigmoid(float x)
    {
        return 1f / (1f + Mathf.Exp(-x));
    }

    // ������Ʈ ���� �� RenderTexture �޸� ����
    // GC�� �ڵ� �������� �ʴ� ����Ƽ�� �޸��̹Ƿ� ������ ���� �ʿ�
    private void OnDestroy()
    {
        if (observationRT != null)
        {
            observationRT.Release();
            Destroy(observationRT);
        }
    }
}
