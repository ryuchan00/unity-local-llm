using System.Collections.Generic;
using UnityEngine;

namespace LocalLLM.Samples
{
    /// <summary>
    /// Minimal IMGUI chat screen. The Editor and standalone builds talk to a local Ollama;
    /// WebGL builds run the model in the browser with WebLLM.
    /// </summary>
    public sealed class ChatWindow : MonoBehaviour
    {
        enum TextLanguage { Auto, English, Japanese }

        const string InputControlName = "ChatInput";
        // Height kept free at the bottom for the HTML input bar on WebGL.
        const float WebInputReservedHeight = 72;

        [SerializeField] LocalLLMSettings settings = new LocalLLMSettings();

        [Header("Chat")]
        [Tooltip("Auto follows the OS or browser language.")]
        [SerializeField] TextLanguage language = TextLanguage.Auto;
        [SerializeField, TextArea(2, 6)] string systemPrompt =
            "You are a friendly innkeeper in a fantasy village. Answer briefly, in the player's language.";
        [SerializeField] int fontSize = 18;
        [Tooltip("Required for Japanese on WebGL: the browser build has no fallback font for it.")]
        [SerializeField] Font font;

        readonly List<ChatMessage> history = new List<ChatMessage>();
        IChatBackend backend;
        ChatTexts texts;
        bool? modelCached;
        string input = string.Empty;
        string streaming;
        string status = string.Empty;
        Vector2 scroll;
        bool scrollToBottom;
        GUIStyle userStyle, assistantStyle, infoStyle;

        void Awake()
        {
            var japanese = language == TextLanguage.Japanese ||
                           (language == TextLanguage.Auto && Application.systemLanguage == SystemLanguage.Japanese);
            texts = japanese ? ChatTexts.Japanese : ChatTexts.English;
            backend = ChatBackendFactory.Create(settings);
        }

        void Start()
        {
            Input.imeCompositionMode = IMECompositionMode.On;
            if (backend is WebLLMBackend webLLM)
                webLLM.IsModelCached(cached => modelCached = cached, _ => modelCached = null);
            if (!WebTextInput.IsAvailable) return;
            WebTextInput.Create(texts.Placeholder, texts.Send);
            WebTextInput.Submitted += Submit;
        }

        void Update()
        {
            if (WebTextInput.IsAvailable) WebTextInput.SetState(backend.IsReady, !backend.IsBusy);
        }

        void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(16, 16, Screen.width - 32, Screen.height - 32));
            DrawToolbar();
            DrawLog();
            if (!backend.IsReady) DrawLoader();
            else if (WebTextInput.IsAvailable) DrawWebInputArea();
            else DrawInput();
            GUILayout.EndArea();
        }

        void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(ChatBackendFactory.UsesWebLLM ? "WebLLM: " + settings.WebLLMModel : "Ollama: " + settings.OllamaModel, infoStyle);
            GUI.enabled = !backend.IsBusy;
            if (GUILayout.Button(texts.Reset, GUILayout.Width(160))) ResetConversation();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void DrawLoader()
        {
            var message = backend.LoadError != null ? texts.Describe(backend.LoadError)
                : backend.Status.Length > 0 ? backend.Status
                : modelCached == true ? texts.LoadCached
                : texts.LoadFirstTime;
            GUILayout.Label(message, infoStyle);
            GUI.enabled = !backend.IsBusy;
            if (GUILayout.Button(texts.Load, GUILayout.Height(fontSize + 16))) backend.Load();
            GUI.enabled = true;
        }

        void DrawLog()
        {
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            foreach (var message in history) DrawMessage(message.Role, message.Content);
            if (streaming != null) DrawMessage(ChatRole.Assistant, streaming.Length > 0 ? streaming : "...");
            GUILayout.EndScrollView();

            if (scrollToBottom && Event.current.type == EventType.Repaint)
            {
                scroll.y = float.MaxValue;
                scrollToBottom = false;
            }
        }

        void DrawMessage(string role, string content)
        {
            var isUser = role == ChatRole.User;
            GUILayout.Label((isUser ? texts.You : texts.Ai) + content, isUser ? userStyle : assistantStyle);
            GUILayout.Space(6);
        }

        void DrawWebInputArea()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(status, infoStyle);
            if (backend.IsBusy && GUILayout.Button(texts.Stop, GUILayout.Width(100))) backend.Cancel();
            GUILayout.EndHorizontal();
            GUILayout.Space(WebInputReservedHeight);
        }

        void DrawInput()
        {
            if (status.Length > 0) GUILayout.Label(status, infoStyle);

            GUILayout.BeginHorizontal();
            var submitByEnter = IsEnterPressed();
            GUI.SetNextControlName(InputControlName);
            input = GUILayout.TextField(input, GUILayout.Height(fontSize + 16), GUILayout.ExpandWidth(true));

            if (backend.IsBusy)
            {
                if (GUILayout.Button(texts.Stop, GUILayout.Width(100), GUILayout.Height(fontSize + 16))) backend.Cancel();
            }
            else if (GUILayout.Button(texts.Send, GUILayout.Width(100), GUILayout.Height(fontSize + 16)) || submitByEnter)
            {
                Submit(input);
            }
            GUILayout.EndHorizontal();
        }

        bool IsEnterPressed()
        {
            var e = Event.current;
            var isEnter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            // Ignore the Enter that confirms an IME conversion.
            var ok = isEnter && GUI.GetNameOfFocusedControl() == InputControlName && string.IsNullOrEmpty(Input.compositionString);
            if (ok) e.Use();
            return ok;
        }

        void Submit(string rawText)
        {
            var text = rawText.Trim();
            if (text.Length == 0 || backend.IsBusy || !backend.IsReady) return;

            history.Add(new ChatMessage(ChatRole.User, text));
            input = string.Empty;
            streaming = string.Empty;
            status = texts.Generating;
            scrollToBottom = true;

            backend.Send(BuildRequestMessages(), ChatOptions.Default, OnToken, OnComplete, OnError);
            if (!WebTextInput.IsAvailable) GUI.FocusControl(InputControlName);
        }

        // The system prompt stays first and history only grows at the end,
        // so the engine can reuse its cache of the shared prefix on every turn.
        List<ChatMessage> BuildRequestMessages()
        {
            var messages = new List<ChatMessage>(history.Count + 1);
            if (!string.IsNullOrWhiteSpace(systemPrompt)) messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
            messages.AddRange(history);
            return messages;
        }

        void OnToken(string token)
        {
            streaming += token;
            scrollToBottom = true;
        }

        void OnComplete(ChatResult result)
        {
            history.Add(new ChatMessage(ChatRole.Assistant, result.Text));
            streaming = null;
            status = texts.Usage(result.Usage);
            scrollToBottom = true;
        }

        void OnError(ChatError error)
        {
            // Keep a partial reply so the log stays readable; otherwise drop the unanswered turn.
            if (!string.IsNullOrEmpty(streaming))
                history.Add(new ChatMessage(ChatRole.Assistant, streaming));
            else if (history.Count > 0 && history[history.Count - 1].Role == ChatRole.User)
                history.RemoveAt(history.Count - 1);
            streaming = null;
            status = texts.Describe(error);
        }

        void ResetConversation()
        {
            history.Clear();
            streaming = null;
            status = string.Empty;
        }

        void OnDestroy()
        {
            if (WebTextInput.IsAvailable) WebTextInput.Submitted -= Submit;
            backend?.Dispose();
        }

        void EnsureStyles()
        {
            if (userStyle != null) return;
            if (font != null) GUI.skin.font = font;
            userStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.85f, 1f) },
            };
            assistantStyle = new GUIStyle(userStyle) { normal = { textColor = Color.white } };
            infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(12, fontSize - 4),
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) },
            };
            GUI.skin.textField.fontSize = fontSize;
            GUI.skin.button.fontSize = fontSize - 2;
        }
    }
}
