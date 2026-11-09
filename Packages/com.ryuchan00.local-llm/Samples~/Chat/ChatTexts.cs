namespace LocalLLM.Samples
{
    /// <summary>Player-facing text for the sample. The library itself only reports ChatErrorKind.</summary>
    sealed class ChatTexts
    {
        public static readonly ChatTexts English = new ChatTexts
        {
            You = "You: ",
            Ai = "AI: ",
            Send = "Send",
            Stop = "Stop",
            Reset = "Reset chat",
            Load = "Load AI",
            Placeholder = "Type a message (Enter to send)",
            Generating = "Generating...",
            LoadFirstTime = "Downloads the AI model (about 1 GB) the first time. Later visits load it from the browser cache.",
            LoadCached = "The AI model is already downloaded.",
            Cancelled = "Stopped.",
            ConnectionFailed = "Cannot reach Ollama. Is `ollama serve` running?",
            ModelNotFound = "Model not found. Run `ollama pull <model>`.",
            WebGPUUnsupported = "This browser does not support WebGPU. Open the page in a recent desktop Chrome or Edge.",
            Failed = "Something went wrong: ",
            UsageFormat = "Prompt {0} tokens in {1:0} ms, reply {2} tokens",
        };

        public static readonly ChatTexts Japanese = new ChatTexts
        {
            You = "あなた: ",
            Ai = "AI: ",
            Send = "送信",
            Stop = "停止",
            Reset = "会話をリセット",
            Load = "AI を読み込む",
            Placeholder = "メッセージを入力（Enter で送信）",
            Generating = "生成中...",
            LoadFirstTime = "初回は AI モデル（約 1GB）をダウンロードします。2 回目以降はブラウザのキャッシュから読み込みます。",
            LoadCached = "AI モデルはダウンロード済みです。",
            Cancelled = "中断しました。",
            ConnectionFailed = "Ollama に接続できません。ollama serve が起動しているか確認してください。",
            ModelNotFound = "モデルが見つかりません。ollama pull <モデル名> を実行してください。",
            WebGPUUnsupported = "このブラウザは WebGPU に対応していません。PC 版 Chrome / Edge の最新版で開いてください。",
            Failed = "エラーが発生しました: ",
            UsageFormat = "プロンプト {0} トークン（{1:0} ms）、返答 {2} トークン",
        };

        public string You, Ai, Send, Stop, Reset, Load, Placeholder, Generating, LoadFirstTime, LoadCached;
        public string Cancelled, ConnectionFailed, ModelNotFound, WebGPUUnsupported, Failed, UsageFormat;

        public string Describe(ChatError error)
        {
            switch (error.Kind)
            {
                case ChatErrorKind.Cancelled: return Cancelled;
                case ChatErrorKind.ConnectionFailed: return ConnectionFailed;
                case ChatErrorKind.ModelNotFound: return ModelNotFound;
                case ChatErrorKind.WebGPUUnsupported: return WebGPUUnsupported;
                default: return Failed + error.Detail;
            }
        }

        public string Usage(ChatUsage usage) =>
            usage.PromptTokens.HasValue && usage.PromptTime.HasValue
                ? string.Format(UsageFormat, usage.PromptTokens, usage.PromptTime.Value.TotalMilliseconds, usage.CompletionTokens)
                : string.Empty;
    }
}
