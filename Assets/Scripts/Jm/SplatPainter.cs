using System.Collections.Generic;
using UnityEngine;

public class SplatPainter : MonoBehaviour
{
    [Header("핵심 연결")]
    public ParticleSystem partSystem;
    public RenderTexture canvasTexture;

    [Header("물감 설정")]
    public Texture2D[] brushSplats;
    public float brushSize = 0.1f;
    public float spreadRadius = 0.03f;
    public float sizeVariance = 0.4f;

    public Color[] paintPalette = new Color[] {
        new Color(1.00f, 0.22f, 0.33f),
        new Color(1.00f, 0.76f, 0.03f),
        new Color(0.00f, 0.64f, 0.91f),
        new Color(0.48f, 0.81f, 0.22f),
        new Color(0.60f, 0.20f, 0.80f)
    };

    private List<ParticleCollisionEvent> colEvents = new List<ParticleCollisionEvent>();
    private Material drawMaterial;

    void Start()
    {
        drawMaterial = new Material(Shader.Find("Custom/SplatBrush"));

        if (canvasTexture != null)
        {
            RenderTexture.active = canvasTexture;
            GL.Clear(true, true, Color.white);
            RenderTexture.active = null;
        }
    }

    void OnParticleCollision(GameObject other)
    {
        if (brushSplats == null || brushSplats.Length == 0) return;

        int numCollisionEvents = partSystem.GetCollisionEvents(other, colEvents);
        if (numCollisionEvents == 0) return;

        RenderTexture.active = canvasTexture;
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, 1, 1, 0);

        for (int i = 0; i < numCollisionEvents; i++)
        {
            Vector3 pos = colEvents[i].intersection;
            Vector3 velocity = colEvents[i].velocity;

            if (Physics.Raycast(pos - velocity.normalized * 0.1f, velocity, out RaycastHit hit))
            {
                if (hit.collider != null)
                {
                    DrawPaint(hit.textureCoord);
                }
            }
        }

        GL.PopMatrix();
        RenderTexture.active = null;
    }

    void DrawPaint(Vector2 centerUv)
    {
        Color randomColor = Color.black;
        if (paintPalette != null && paintPalette.Length > 0)
        {
            randomColor = paintPalette[Random.Range(0, paintPalette.Length)];
        }
        drawMaterial.color = randomColor;

        Texture2D randomSplat = brushSplats[Random.Range(0, brushSplats.Length)];

        Vector2 randomOffset = Random.insideUnitCircle * spreadRadius;
        Vector2 finalUV = centerUv + randomOffset;

        float randomScale = 1f + Random.Range(-sizeVariance, sizeVariance);
        float currentBrushSize = brushSize * randomScale;

        Rect rect = new Rect(finalUV.x - currentBrushSize / 2, (1 - finalUV.y) - currentBrushSize / 2, currentBrushSize, currentBrushSize);
        Graphics.DrawTexture(rect, randomSplat, drawMaterial);
    }
}