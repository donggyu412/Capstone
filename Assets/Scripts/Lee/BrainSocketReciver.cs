using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class BrainSocketReceiver : MonoBehaviour
{
    public int port = 9000;

    [Header("연결")]
    public DrawingController drawingController;   // ← Inspector에서 연결

    private TcpListener listener;
    private Thread listenThread;
    private bool running = false;

    // 받은 stroke 묶음을 메인 스레드에서 꺼내 쓸 버퍼
    private List<float[]> pendingBatch = null;
    private readonly object batchLock = new object();

    void Start()
    {
        running = true;
        listenThread = new Thread(ListenLoop);
        listenThread.IsBackground = true;
        listenThread.Start();
        Debug.Log($"[Socket] 수신 대기 중 → 포트 {port}");
    }

    void Update()
    {
        // 메인 스레드에서만 Unity API(코루틴) 호출 가능
        List<float[]> batch = null;
        lock (batchLock)
        {
            if (pendingBatch != null)
            {
                batch = pendingBatch;
                pendingBatch = null;
            }
        }

        if (batch != null && drawingController != null)
        {
            Debug.Log($"[Socket] {batch.Count}개 stroke로 드로잉 시작");
            drawingController.StartStrokeDrawing(batch);
        }
    }

    void ListenLoop()
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        while (running)
        {
            try
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    Debug.Log("[Socket] Python Brain 연결됨!");

                    byte[] header = new byte[4];
                    int read = 0;
                    while (read < 4)
                        read += stream.Read(header, read, 4 - read);

                    Array.Reverse(header);  // big-endian (struct.pack("!I", ...))
                    int dataLength = BitConverter.ToInt32(header, 0);

                    byte[] buffer = new byte[dataLength];
                    int totalRead = 0;
                    while (totalRead < dataLength)
                    {
                        int n = stream.Read(buffer, totalRead, dataLength - totalRead);
                        if (n <= 0) break;
                        totalRead += n;
                    }

                    string json = Encoding.UTF8.GetString(buffer);
                    ParseAndStore(json);
                }
            }
            catch (Exception e)
            {
                if (running) Debug.LogWarning($"[Socket] 오류: {e.Message}");
            }
        }
    }

    private void ParseAndStore(string json)
    {
        try
        {
            var payload = JsonConvert.DeserializeObject<StrokePayload>(json);
            lock (batchLock)
            {
                pendingBatch = new List<float[]>(payload.strokes);
            }
            Debug.Log($"[Socket] {payload.strokes.Length}개 stroke 수신 완료 (메인 스레드 처리 대기)");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Socket] JSON 파싱 실패: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        listener?.Stop();
    }

    [Serializable]
    private class StrokePayload
    {
        public float[][] strokes;
    }
}