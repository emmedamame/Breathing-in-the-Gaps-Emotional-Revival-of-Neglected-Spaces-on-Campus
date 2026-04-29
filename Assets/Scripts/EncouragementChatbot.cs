using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class EncouragementChatbot : MonoBehaviour
{
    [Header("GLM API")]
    [SerializeField] private string apiKey = "";
    [SerializeField] private string model = "glm-4-flash";
    [SerializeField] private string apiUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions";

    [Header("Persona")]
    [SerializeField] private string persona = "";

    private float _lastEliminateTime = -1f;

    private void OnEnable()
    {
        BallEliminationEvents.OnBallEliminated += OnBallEliminated;
    }

    private void OnDisable()
    {
        BallEliminationEvents.OnBallEliminated -= OnBallEliminated;
    }

    private void OnBallEliminated(float timeNow, string ballName)
    {
        var interval = _lastEliminateTime < 0f ? -1f : timeNow - _lastEliminateTime;
        _lastEliminateTime = timeNow;
        StartCoroutine(RequestEncouragement(interval, ballName));
    }

    private IEnumerator RequestEncouragement(float intervalSeconds, string ballName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.LogWarning("[EncouragementChatbot] API Key is empty.");
            yield break;
        }

        var tone = GetTone(intervalSeconds);
        var prompt = BuildPrompt(intervalSeconds, tone, ballName);
        var body = new ChatRequestBody
        {
            model = model,
            messages = new ChatMessage[]
            {
                new ChatMessage
                {
                    role = "system",
                    content = "You are a motivational chatbot. Return exactly one short Chinese sentence."
                },
                new ChatMessage
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        var json = JsonUtility.ToJson(body);
        using var req = new UnityWebRequest(apiUrl, "POST");
        var bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("[EncouragementChatbot] Request failed: " + req.error);
            yield break;
        }

        var responseText = req.downloadHandler.text;
        var response = JsonUtility.FromJson<ChatResponseBody>(responseText);
        if (response != null &&
            response.choices != null &&
            response.choices.Length > 0 &&
            response.choices[0] != null &&
            response.choices[0].message != null &&
            !string.IsNullOrWhiteSpace(response.choices[0].message.content))
        {
            Debug.Log("[Chatbot] " + response.choices[0].message.content.Trim());
        }
        else
        {
            Debug.Log("[Chatbot] " + responseText);
        }
    }

    private string BuildPrompt(float intervalSeconds, string tone, string ballName)
    {
        var personaText = string.IsNullOrWhiteSpace(persona) ? "热情教练" : persona;
        var intervalText = intervalSeconds < 0f ? "首次消除" : intervalSeconds.ToString("F2") + "秒";
        return "你的人格是：" + personaText +
               "。玩家刚消除了球：" + ballName +
               "。距离上次消除间隔：" + intervalText +
               "。本次语气要求：" + tone +
               "。请给一句简短中文鼓励，最多20字。";
    }

    private string GetTone(float intervalSeconds)
    {
        if (intervalSeconds < 0f) return "偏爱慕，温柔夸奖";
        if (intervalSeconds <= 1.5f) return "爱慕，崇拜式夸奖";
        if (intervalSeconds <= 3.5f) return "热情鼓励";
        if (intervalSeconds <= 6f) return "平静鼓励";
        if (intervalSeconds <= 10f) return "轻微嘲讽后鼓励";
        return "嘲讽但不恶毒，最后仍鼓励";
    }

    [System.Serializable]
    private class ChatRequestBody
    {
        public string model;
        public ChatMessage[] messages;
    }

    [System.Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class ChatResponseBody
    {
        public ChatChoice[] choices;
    }

    [System.Serializable]
    private class ChatChoice
    {
        public ChatMessage message;
    }
}
