using UnityEngine;

public class DrawOnCanvas : MonoBehaviour
{
    public int textureWidth = 256;
    public int textureHeight = 192;

    private Texture2D canvasTexture;
    private Renderer canvasRenderer;

    void Start()
    {
        InitCanvas();
    }

    public void InitCanvas()
    {
        canvasTexture = new Texture2D(textureWidth, textureHeight);
        ResetCanvas();
        canvasRenderer = GetComponent<Renderer>();
        canvasRenderer.material.mainTexture = canvasTexture;
    }

    public void ResetCanvas()
    {
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        canvasTexture.SetPixels(pixels);
        canvasTexture.Apply();
    }

    public void DrawAtWorldPos(Vector3 worldPos, float brushSize, Color color)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        float u = localPos.x + 0.5f;
        float v = localPos.y + 0.5f;

        if (u < 0 || u > 1 || v < 0 || v > 1) return;

        int px = (int)(u * textureWidth);
        int py = (int)(v * textureHeight);
        int radius = Mathf.Max(1, (int)brushSize);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy <= radius * radius)
                {
                    int nx = px + dx;
                    int ny = py + dy;
                    if (nx >= 0 && nx < textureWidth && ny >= 0 && ny < textureHeight)
                        canvasTexture.SetPixel(nx, ny, color);
                }
            }
        }
        canvasTexture.Apply();
    }

    public float[] GetCanvasArray()
    {
        Color[] pixels = canvasTexture.GetPixels();
        float[] result = new float[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
            result[i] = pixels[i].grayscale;
        return result;
    }
}