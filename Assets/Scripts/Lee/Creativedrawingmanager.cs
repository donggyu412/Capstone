using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// 단어 입력 -> Claude API 꿈 스토리 프롬프트 생성 -> GPT Image 이미지 생성 -> 로봇팔 드로잉
/// </summary>
public class CreativeDrawingManager : MonoBehaviour
{
    [Header("API 키")]
    public string claudeApiKey = "여기에_Claude_API_키_입력";
    public string openAiApiKey = "여기에_OpenAI_API_키_입력";

    [Header("UI 연결")]
    public TMP_InputField wordInputField;
    public Button addWordButton;
    public Button startDrawingButton;
    public Button clearWordsButton;
    public Transform wordTagContainer;
    public GameObject wordTagPrefab;
    public TMP_Text statusText;
    public TMP_Text promptDisplayText;
    public RawImage previewImage1;
    public RawImage previewImage2;
    public RawImage previewImage3;

    [Header("연결")]
    public DrawingController drawingController;

    [Header("꿈 이미지 설정")]
    [Tooltip("생성할 꿈 이미지 수 (1~3)")]
    [Range(1, 3)] public int dreamImageCount = 1;

    private List<string> words = new List<string>();
    private List<GameObject> wordTagObjects = new List<GameObject>();
    private bool isProcessing = false;
    private List<Texture2D> dreamImages = new List<Texture2D>();
    private List<string> dreamPrompts = new List<string>();

    void Start()
    {
        addWordButton.onClick.AddListener(AddWord);
        startDrawingButton.onClick.AddListener(OnStartButtonClicked);
        clearWordsButton.onClick.AddListener(ClearWords);
        wordInputField.onSubmit.AddListener(_ => AddWord());
        UpdateUI();
    }

    void AddWord()
    {
        string word = wordInputField.text.Trim();
        if (string.IsNullOrEmpty(word)) return;
        if (words.Count >= 10) { SetStatus("Maximum 10 words allowed."); return; }

        words.Add(word);
        wordInputField.text = "";
        wordInputField.ActivateInputField();

        if (wordTagPrefab != null && wordTagContainer != null)
        {
            GameObject tag = Instantiate(wordTagPrefab, wordTagContainer);
            TMP_Text tagText = tag.GetComponentInChildren<TMP_Text>();
            if (tagText != null) tagText.text = word;

            Button deleteBtn = tag.GetComponentInChildren<Button>();
            if (deleteBtn != null)
            {
                GameObject tagRef = tag;
                int idx = words.Count - 1;
                deleteBtn.onClick.AddListener(() => RemoveWord(idx, tagRef));
            }
            wordTagObjects.Add(tag);
        }

        UpdateUI();
        SetStatus(words.Count + " word(s) added");
    }

    void RemoveWord(int index, GameObject tagObject)
    {
        if (index < words.Count) words.RemoveAt(index);
        if (tagObject != null) { wordTagObjects.Remove(tagObject); Destroy(tagObject); }
        UpdateUI();
    }

    void ClearWords()
    {
        words.Clear();
        foreach (var tag in wordTagObjects) if (tag != null) Destroy(tag);
        wordTagObjects.Clear();
        UpdateUI();
        SetStatus("Cleared.");
    }

    void OnStartButtonClicked()
    {
        if (isProcessing) return;
        if (words.Count < 1) { SetStatus("Please enter at least 1 word."); return; }
        StartCoroutine(ProcessPipeline());
    }

    IEnumerator ProcessPipeline()
    {
        isProcessing = true;
        startDrawingButton.interactable = false;
        dreamImages.Clear();
        dreamPrompts.Clear();

        // 1단계: Claude API로 꿈 스토리 프롬프트 생성
        SetStatus("Claude AI is dreaming... creating " + dreamImageCount + " dream scene(s)...");
        yield return StartCoroutine(GenerateDreamPromptsWithClaude(words));

        if (dreamPrompts.Count == 0)
        {
            SetStatus("Failed to generate dream prompts. Please try again.");
            isProcessing = false;
            startDrawingButton.interactable = true;
            yield break;
        }

        if (promptDisplayText != null)
            promptDisplayText.text = "Dream Story:\n" + string.Join("\n\n", dreamPrompts);

        // 2단계: 각 프롬프트로 이미지 생성
        RawImage[] previews = { previewImage1, previewImage2, previewImage3 };
        for (int i = 0; i < dreamPrompts.Count; i++)
        {
            SetStatus("Generating dream image " + (i + 1) + "/" + dreamPrompts.Count + "...");
            Texture2D img = null;
            yield return StartCoroutine(GenerateImageWithDallE(dreamPrompts[i], result => img = result));

            if (img == null)
            {
                SetStatus("Failed to generate image " + (i + 1) + ". Skipping...");
                continue;
            }

            dreamImages.Add(img);

            if (i < previews.Length && previews[i] != null)
                previews[i].texture = img;

            Debug.Log("꿈 이미지 " + (i + 1) + " 생성 완료");
        }

        if (dreamImages.Count == 0)
        {
            SetStatus("No images generated. Please try again.");
            isProcessing = false;
            startDrawingButton.interactable = true;
            yield break;
        }

        // 3단계: 로봇팔 드로잉 시작
        SetStatus("Robot arm is starting to draw the dream...");
        if (drawingController != null)
            drawingController.StartDreamDrawing(dreamImages);

        isProcessing = false;
        startDrawingButton.interactable = true;
    }

    // ─────────────────────────────────────────────────────────────
    // Claude API: 키워드로 꿈 스토리 N장 프롬프트 생성
    // ─────────────────────────────────────────────────────────────
    IEnumerator GenerateDreamPromptsWithClaude(List<string> inputWords)
    {
        string wordList = string.Join(", ", inputWords);
        string userMessage =
    "Create a simple DALL-E image prompt for the keyword: " + wordList + "\n\n" +
    "Requirements:\n" +
    "1. Extremely simple, minimal illustration\n" +
    "2. Only 2-3 flat colors, no gradients\n" +
    "3. Bold black outlines, thick strokes\n" +
    "4. Pure white background\n" +
    "5. Single centered object, nothing else\n" +
    "6. Like a simple coloring book page\n\n" +
    "Output ONLY the prompt, nothing else.";

        string requestBody = "{" +
            "\"model\": \"claude-sonnet-4-20250514\"," +
            "\"max_tokens\": 800," +
            "\"messages\": [{\"role\": \"user\", \"content\": \"" + EscapeJson(userMessage) + "\"}]" +
            "}";

        UnityWebRequest request = new UnityWebRequest("https://api.anthropic.com/v1/messages", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-api-key", claudeApiKey);
        request.SetRequestHeader("anthropic-version", "2023-06-01");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Claude API 오류: " + request.error);
            yield break;
        }

        string json = request.downloadHandler.text;
        int textStart = json.IndexOf("\"text\":\"") + 8;
        int textEnd = json.IndexOf("\"", textStart);

        if (textStart > 8 && textEnd > textStart)
        {
            string raw = json.Substring(textStart, textEnd - textStart);
            raw = UnescapeJson(raw);
            Debug.Log("Claude 꿈 스토리 원문:\n" + raw);

            string[] parts = raw.Split(new string[] { "---" }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    dreamPrompts.Add(trimmed);
            }

            Debug.Log("생성된 꿈 프롬프트 수: " + dreamPrompts.Count);
        }
        else
        {
            Debug.LogError("Claude 응답 파싱 실패: " + json);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // GPT Image API: 프롬프트로 이미지 생성
    // ─────────────────────────────────────────────────────────────
    IEnumerator GenerateImageWithDallE(string prompt, System.Action<Texture2D> callback)
    {
        string requestBody = "{" +
            "\"model\": \"gpt-image-1\"," +
            "\"prompt\": \"" + EscapeJson(prompt) + "\"," +
            "\"n\": 1," +
            "\"size\": \"1024x1024\"," +
            "\"quality\": \"low\"" +
            "}";

        UnityWebRequest request = new UnityWebRequest("https://api.openai.com/v1/images/generations", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("GPT Image API 오류: " + request.error + "\n" + request.downloadHandler.text);
            callback(null);
            yield break;
        }

        string json = request.downloadHandler.text;
        string b64Data = null;
        string imageUrlData = null;

        try
        {
            string[] searchKeys = { "\"b64_json\":\"", "\"b64_json\": \"" };
            int b64Start = -1;
            foreach (var key in searchKeys)
            {
                int idx = json.IndexOf(key);
                if (idx >= 0) { b64Start = idx + key.Length; break; }
            }

            if (b64Start >= 0)
            {
                int b64End = b64Start;
                while (b64End < json.Length && json[b64End] != '"') b64End++;
                b64Data = json.Substring(b64Start, b64End - b64Start)
                    .Replace("\\n", "").Replace("\n", "").Replace(" ", "").Replace("\r", "");
                Debug.Log("Base64 데이터 길이: " + b64Data.Length);
            }
            else
            {
                string[] urlKeys = { "\"url\":\"", "\"url\": \"" };
                foreach (var key in urlKeys)
                {
                    int idx = json.IndexOf(key);
                    if (idx >= 0)
                    {
                        int urlStart = idx + key.Length;
                        int urlEnd = json.IndexOf("\"", urlStart);
                        imageUrlData = UnescapeJson(json.Substring(urlStart, urlEnd - urlStart));
                        break;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("이미지 파싱 예외: " + e.Message);
            callback(null);
            yield break;
        }

        if (b64Data != null)
        {
            try
            {
                byte[] imageBytes = System.Convert.FromBase64String(b64Data);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(imageBytes);
                Debug.Log("이미지 생성 완료! 크기: " + texture.width + "x" + texture.height);
                callback(texture);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Base64 디코딩 실패: " + e.Message);
                callback(null);
            }
        }
        else if (imageUrlData != null)
        {
            yield return StartCoroutine(DownloadImage(imageUrlData, callback));
        }
        else
        {
            Debug.LogError("이미지 데이터를 찾을 수 없음");
            callback(null);
        }
    }

    IEnumerator DownloadImage(string url, System.Action<Texture2D> callback)
    {
        SetStatus("Downloading image...");
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("이미지 다운로드 오류: " + request.error);
            callback(null);
            yield break;
        }
        callback(DownloadHandlerTexture.GetContent(request));
    }

    void UpdateUI()
    {
        startDrawingButton.interactable = words.Count >= 1 && !isProcessing;
    }

    void SetStatus(string message)
    {
        Debug.Log("[CreativeDrawing] " + message);
        if (statusText != null) statusText.text = message;
    }

    string EscapeJson(string str) =>
        str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

    string UnescapeJson(string str) =>
        str.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
}