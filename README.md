# Local LLM for Unity (Ollama + WebLLM)

Chat with a local LLM from Unity through one C# interface:

- **Editor and standalone builds** talk to [Ollama](https://ollama.com) running on the player's machine.
- **WebGL builds** run the model inside the browser with [WebLLM](https://github.com/mlc-ai/web-llm) and WebGPU. No server, no API key, and messages never leave the machine.

**[Try the WebGL demo](https://ryuchan00.github.io/unity-local-llm/)** (desktop Chrome or Edge; downloads about 1 GB on first load).

[日本語版 README](README.ja.md)

## Features

- Streaming replies, cancellation, and JSON-schema-constrained output on both engines
- Callback and `async`/`await` APIs; everything runs on the main thread, so WebGL works
- Token usage and prompt timing, to check that the engine reuses its prompt cache
- WebLLM is bundled and hash-checked: no CDN at runtime, works under a strict Content Security Policy
- Default WebGL model files are pinned to exact commits and verified with SRI hashes
- An HTML text box for WebGL, so players can type Japanese, Chinese or Korean with an IME
- No dependencies outside Unity

## Requirements

- Unity 2022.3 or later
- Editor / standalone: [Ollama](https://ollama.com) with a model pulled, e.g. `ollama pull llama3.2:3b`
- WebGL: a browser with WebGPU (recent desktop Chrome or Edge)

## Installation

In Package Manager, choose **Add package from git URL** and enter:

```
https://github.com/ryuchan00/unity-local-llm.git?path=Packages/com.ryuchan00.local-llm#v0.1.0
```

Tags are protected in this repository, but for the strongest guarantee pin a commit instead of a tag:

```
https://github.com/ryuchan00/unity-local-llm.git?path=Packages/com.ryuchan00.local-llm#<commit-sha>
```

Then import the **Chat** sample from the package's Samples tab and open `Chat.unity`.

## Quick start

```csharp
using System.Collections.Generic;
using LocalLLM;
using UnityEngine;

public class Innkeeper : MonoBehaviour
{
    [SerializeField] LocalLLMSettings settings = new LocalLLMSettings();
    IChatBackend backend;

    void Awake()
    {
        // WebLLMBackend on WebGL, OllamaBackend everywhere else.
        backend = ChatBackendFactory.Create(settings);
        backend.Load(); // WebGL: downloads the model on first use. Ollama: no-op.
    }

    async void Talk()
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "You are an innkeeper. Answer briefly."),
            new ChatMessage(ChatRole.User, "Do you have a room?"),
        };
        try
        {
            var result = await backend.SendAsync(messages, onToken: token => Debug.Log(token));
            Debug.Log(result.Text);
        }
        catch (ChatException e) when (e.Error.Kind == ChatErrorKind.ConnectionFailed)
        {
            Debug.Log("Start Ollama first.");
        }
    }
}
```

Wait for `backend.IsReady` before sending. `backend.Status` reports download progress and `backend.LoadError` tells why loading failed.

The callback form is also available:

```csharp
backend.Send(messages, ChatOptions.Default, onToken, onComplete: result => { }, onError: error => { });
```

After `Send`, `onToken` is called zero or more times, then exactly one of `onComplete` or `onError`. Exceptions thrown from your callbacks are logged and do not break the backend. `Cancel` reports `ChatErrorKind.Cancelled` once the engine has stopped, which can be a frame later.

Backends implement `IDisposable`. Dispose them when the owning object is destroyed: this stops any reply without calling its callbacks, and on WebGL frees the model from GPU memory.

## Options

```csharp
var options = ChatOptions.Default
    .WithTemperature(0.2f)
    .WithMaxTokens(256)
    .WithJsonSchema(schemaJson) // constrain the reply to JSON
    .WithThink(false);          // thinking models on Ollama; ignored by WebLLM
```

`ChatOptions` is immutable; each `With` method returns a new instance.

| `LocalLLMSettings` | Default | Notes |
|---|---|---|
| `OllamaUrl` | `http://localhost:11434` | Only `http` and `https` URLs are accepted. |
| `OllamaModel` | `llama3.2:3b` | |
| `OllamaKeepAlive` | `30m` | Unloading the model also drops its prompt cache. |
| `OllamaContextLength` | `4096` | Keep it fixed: changing it reloads the model. |
| `OllamaTimeoutSeconds` | `600` | Includes loading the model. `0` waits forever. Reported as `ChatErrorKind.Timeout`. |
| `WebLLMModel` | `Qwen2.5-1.5B-Instruct-q4f16_1-MLC` | Falls back to the `q4f32_1` variant on GPUs without `shader-f16`. |
| `WebLLMAppConfigJson` | empty | A full WebLLM `appConfig`, e.g. to self-host the model. Treat it like code; see Security. |

Define `LOCALLLM_FORCE_OLLAMA` to make a WebGL build use Ollama instead of WebLLM (for testing on machines without WebGPU).

## Getting the most from the prompt cache

Neither engine has an explicit caching API, but both reuse the computation for a prompt prefix that matches the previous request. Measured with Ollama 0.20 and `llama3.2:3b` on a 3,500-token system prompt, processing the prompt took 764 ms the first time and 8 to 40 ms when the next request only appended to the conversation. Changing a single character at the start made it take the full time again. In the WebLLM demo, the second message processed only the 18 new prompt tokens and started replying after 389 ms.

- Keep the system prompt fixed and append to the history; put changing state (time, game state) in the latest user message.
- The engine remembers only the previous prompt. Alternating between NPCs with different system prompts misses the cache every time.
- Check `ChatResult.Usage.PromptTime`: a short time on a long prompt means the cache was used. Ollama reports the whole prompt in `PromptTokens`, WebLLM only the part it did not reuse.
- Some models reuse less. With `gemma3:4b` we saw no speed-up, possibly because of its sliding-window attention.

## WebGL notes

- **Build step**: packages cannot contain `StreamingAssets`, so after every WebGL build the package copies WebLLM into `<build>/StreamingAssets/local-llm/`. It refuses to copy a file whose SHA-256 differs from the vendored release.
- **Text input**: Unity WebGL text fields cannot receive IME input. `WebTextInput` overlays an HTML text box; see the Chat sample.
- **Model cache**: the browser keeps the model after the first download. `WebLLMBackend.IsModelCached` and `DeleteCachedModel` let you show the right message or free the space.
- **One backend at a time**: jslib callbacks are static, so create a single `WebLLMBackend` and dispose it before creating another.
- **Content Security Policy**: no `eval` or inline script is needed. The demo page uses `script-src 'self' 'wasm-unsafe-eval'`; see [its template](Assets/WebGLTemplates/LocalLLM/index.html).
- **Hosting**: if you use Unity's compression, serve the build with a `Content-Encoding` header. The decompression fallback loads the framework from a `blob:` URL, which `script-src 'self'` blocks. The demo is built uncompressed and relies on GitHub Pages compressing responses.

## Security

- **No third-party code is fetched at runtime.** WebLLM 0.2.86 is vendored from the npm registry after checking its SHA-512 integrity, and every WebGL build checks the SHA-256 of all bundled files (WebLLM, the loader with the model pins, and the license) before copying them; see [Third Party Notices](Packages/com.ryuchan00.local-llm/Third%20Party%20Notices.md).
- **Default model files are pinned.** The model weights (Hugging Face) and the model's WebGPU library (WebLLM's GitHub repository) are requested at fixed commits. WebLLM checks the config, tokenizer and WebGPU library against SHA-384 hashes; the weights are pinned by commit only and are not hashed. Other WebLLM model ids are fetched from WebLLM's default, unpinned URLs, with a console warning. To avoid third-party hosts entirely, serve the model yourself and pass your own `WebLLMAppConfigJson`.
- **A custom `WebLLMAppConfigJson` is code.** Its `model_lib` is WebAssembly that runs in your page. Only use configs you control, and add `integrity` hashes; the loader warns when `integrity.model_lib` is missing.
- **Treat model output as untrusted.** Players can steer the reply through what they type. If replies drive game actions, constrain them with a JSON schema and validate them against your game rules before acting. A system prompt is not a security boundary. The schema itself must be a single JSON object; anything else is rejected, so it cannot add fields to the request.
- **Keep Ollama local.** Ollama has no authentication. Do not expose it with `OLLAMA_HOST=0.0.0.0` or `OLLAMA_ORIGINS=*`. The library warns when it talks to a remote host over plain `http`.
- **Logs stay small.** The library logs endpoints and error kinds, never conversation text.

Please report vulnerabilities privately through GitHub's **Report a vulnerability** button rather than in a public issue.

## License

MIT. WebLLM is Apache-2.0 and the sample font (BIZ UDGothic) is SIL OFL 1.1; see [Third Party Notices](Packages/com.ryuchan00.local-llm/Third%20Party%20Notices.md).
