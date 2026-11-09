# Local LLM for Unity（Ollama + WebLLM）

Unity から、手元で動く LLM と会話するためのパッケージです。
一つの C# インターフェースで、二つの実行環境を切り替えます。

- **エディタと PC 版**：プレイヤーの PC で動く [Ollama](https://ollama.com) につなぎます。
- **WebGL 版**：[WebLLM](https://github.com/mlc-ai/web-llm) と WebGPU で、ブラウザの中でモデルを動かします。サーバーも API キーも要らず、会話の内容は PC の外に出ません。

**[WebGL のデモを試す](https://ryuchan00.github.io/unity-local-llm/)**（PC 版の Chrome か Edge。初回は約 1GB をダウンロードします）

[English README](README.md)

## 特徴

- 両方の実行環境で、ストリーミング、中断、JSON Schema による出力の制約が使えます。
- コールバックと `async`/`await` の両方で書けます。すべてメインスレッドで動くので、WebGL でも使えます。
- トークン数とプロンプトの処理時間を返します。プロンプトのキャッシュが効いているかを確かめられます。
- WebLLM はパッケージに同梱し、ハッシュで検証しています。実行時に CDN を使わず、厳しい CSP の下でも動きます。
- WebGL の既定のモデルは、ファイルの取得先を特定のコミットに固定し、SRI ハッシュで検証します。
- WebGL で IME を使った日本語入力ができる、HTML の入力欄を用意しています。
- Unity 以外への依存はありません。

## 必要なもの

- Unity 2022.3 以降
- エディタと PC 版：[Ollama](https://ollama.com) と、取得済みのモデル（例：`ollama pull llama3.2:3b`）
- WebGL：WebGPU が使えるブラウザ（PC 版の Chrome か Edge の最新版）

## 導入

Package Manager の **Add package from git URL** に、次の URL を入れます。

```
https://github.com/ryuchan00/unity-local-llm.git?path=Packages/com.ryuchan00.local-llm#v0.1.0
```

このリポジトリのタグは保護していますが、より確実にするなら、タグの代わりにコミットのハッシュを指定してください。

```
https://github.com/ryuchan00/unity-local-llm.git?path=Packages/com.ryuchan00.local-llm#<commit-sha>
```

導入後、パッケージの Samples タブから **Chat** を取り込み、`Chat.unity` を開くと動作を確かめられます。

## 使い方

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
        // WebGL なら WebLLMBackend、それ以外なら OllamaBackend を返す。
        backend = ChatBackendFactory.Create(settings);
        backend.Load(); // WebGL では初回にモデルをダウンロードする。Ollama では何もしない。
    }

    async void Talk()
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "あなたは宿屋の主人です。短く答えてください。"),
            new ChatMessage(ChatRole.User, "部屋は空いていますか？"),
        };
        try
        {
            var result = await backend.SendAsync(messages, onToken: token => Debug.Log(token));
            Debug.Log(result.Text);
        }
        catch (ChatException e) when (e.Error.Kind == ChatErrorKind.ConnectionFailed)
        {
            Debug.Log("先に Ollama を起動してください。");
        }
    }
}
```

送る前に `backend.IsReady` が true になるのを待ってください。
ダウンロードの進み具合は `backend.Status` で、読み込みに失敗した理由は `backend.LoadError` で分かります。

コールバックでも書けます。

```csharp
backend.Send(messages, ChatOptions.Default, onToken, onComplete: result => { }, onError: error => { });
```

`Send` のあと、`onToken` が 0 回以上呼ばれ、続いて `onComplete` か `onError` のどちらかが必ず一度だけ呼ばれます。
コールバックの中で例外が起きても、ログに出すだけで、バックエンドの状態は壊れません。
`Cancel` は、エンジンが止まった時点で `ChatErrorKind.Cancelled` を返します。1 フレーム遅れることがあります。

バックエンドは `IDisposable` を実装しています。持ち主のオブジェクトを破棄するときに `Dispose` してください。返答の途中ならコールバックを呼ばずに止め、WebGL ではモデルを GPU のメモリから解放します。
エラーは `ChatErrorKind`（Cancelled、ConnectionFailed、ModelNotFound、WebGPUUnsupported など）で分岐できます。
プレイヤーに見せる文言は、サンプルの `ChatTexts` のようにゲーム側で用意してください。

## 生成の設定

```csharp
var options = ChatOptions.Default
    .WithTemperature(0.2f)
    .WithMaxTokens(256)
    .WithJsonSchema(schemaJson) // 返答を JSON に制約する
    .WithThink(false);          // Ollama の thinking モデル用。WebLLM では無視される
```

`ChatOptions` は変更できないオブジェクトで、`With` で始まるメソッドは新しいインスタンスを返します。

| `LocalLLMSettings` | 既定値 | 補足 |
|---|---|---|
| `OllamaUrl` | `http://localhost:11434` | `http` と `https` だけを受け付けます。 |
| `OllamaModel` | `llama3.2:3b` | |
| `OllamaKeepAlive` | `30m` | モデルがメモリから降ろされると、プロンプトのキャッシュも消えます。 |
| `OllamaContextLength` | `4096` | 固定してください。変えるとモデルが読み込み直されます。 |
| `OllamaTimeoutSeconds` | `600` | モデルの読み込みも含みます。`0` なら待ち続けます。超えると `ChatErrorKind.Timeout` になります。 |
| `WebLLMModel` | `Qwen2.5-1.5B-Instruct-q4f16_1-MLC` | `shader-f16` に対応しない GPU では `q4f32_1` 版に切り替えます。 |
| `WebLLMAppConfigJson` | 空 | WebLLM の `appConfig` 全体。モデルを自分のサーバーから配信するときに使います。コードと同じ扱いが必要です（セキュリティの節）。 |
| `WebLLMBaseUrl` | 空 | `local-llm-loader.js` と `web-llm.js` を置いたフォルダの URL。空なら `StreamingAssets/local-llm/` です。配信先が StreamingAssets を扱えない場合に、自分で管理しているオリジンを指定してください。 |

`LOCALLLM_FORCE_OLLAMA` を定義すると、WebGL 版でも WebLLM の代わりに Ollama を使います（WebGPU のない PC で試すとき用）。

## プロンプトのキャッシュを効かせる

どちらの実行環境にもキャッシュを指定する API はありませんが、直前のリクエストと先頭が一致する部分は、計算を再利用します。
Ollama 0.20 と `llama3.2:3b` で、約 3,500 トークンの system プロンプトを使って測ったところ、プロンプトの処理は初回に 764 ms かかり、会話を後ろに足しただけの次のリクエストでは 8〜40 ms でした。
先頭を一文字変えると、また全体の時間がかかりました。
WebLLM のデモでも、2 回目の発言ではプロンプトのうち新しく足した 18 トークンだけを処理し、389 ms で返答が始まりました。

- system プロンプトは固定し、履歴は後ろに足していきます。時刻やゲームの状態のように毎回変わる情報は、最後のユーザー発言に入れます。
- 覚えているのは直前のプロンプトだけです。system プロンプトの違う NPC に交互に話しかけると、毎回キャッシュが外れます。
- `ChatResult.Usage.PromptTime` を見てください。長いプロンプトなのに短ければ、キャッシュが効いています。`PromptTokens` は、Ollama ではプロンプト全体、WebLLM では再利用しなかった部分だけを数えます。
- モデルによって効き方が違います。`gemma3:4b` では速くなりませんでした。sliding window attention を使うモデルだからだと考えていますが、確かめてはいません。

## WebGL について

- **ビルド後の処理**：パッケージには `StreamingAssets` を置けないので、WebGL のビルドのたびに、パッケージが WebLLM を `<ビルド先>/StreamingAssets/local-llm/` にコピーします。SHA-256 が同梱時の値と違う場合は、コピーせずにビルドを失敗させます。
- **文字入力**：Unity の WebGL のテキスト欄は IME の入力を受け取れません。`WebTextInput` が HTML の入力欄を重ねて表示します。使い方はサンプルを見てください。
- **モデルのキャッシュ**：ブラウザは初回にダウンロードしたモデルを保存します。`WebLLMBackend.IsModelCached` と `DeleteCachedModel` で、表示を出し分けたり容量を空けたりできます。
- **同時に使えるのは一つ**：jslib のコールバックは static なので、`WebLLMBackend` は一つだけ作ってください。作り直すときは、前のものを `Dispose` してください。
- **Content Security Policy**：`eval` もインラインスクリプトも要りません。デモのページは `script-src 'self' 'wasm-unsafe-eval'` で動いています。[テンプレート](Assets/WebGLTemplates/LocalLLM/index.html)を参考にしてください。
- **配信**：Unity の圧縮を使う場合は、`Content-Encoding` を返せるサーバーで配信してください。展開の代替処理（decompression fallback）は `blob:` の URL からスクリプトを読み込むので、`script-src 'self'` と両立しません。デモは圧縮せずに出力し、GitHub Pages 側の圧縮に任せています。

## セキュリティ

- **実行時に外部のコードを取得しません。** WebLLM 0.2.86 は、npm レジストリの SHA-512 と一致することを確かめてから同梱しています。WebGL のビルドでは、同梱したすべてのファイル（WebLLM、モデルの固定情報を持つローダー、ライセンス）の SHA-256 を確かめてからコピーします（[Third Party Notices](Packages/com.ryuchan00.local-llm/Third%20Party%20Notices.md)）。
- **既定のモデルの取得先は固定しています。** モデルの重み（Hugging Face）と、モデルの WebGPU ライブラリ（WebLLM の GitHub リポジトリ）は、特定のコミットから取得します。設定ファイル、トークナイザー、WebGPU ライブラリは SHA-384 で検証します。重みはコミットを固定しているだけで、ハッシュでは検証していません。ほかの WebLLM のモデル ID を選ぶと、WebLLM の既定の（固定していない）URL から取得し、コンソールに警告を出します。外部のホストを使いたくない場合は、モデルを自分で配信し、`WebLLMAppConfigJson` を指定してください。
- **独自の `WebLLMAppConfigJson` はコードと同じです。** その `model_lib` は、ページの中で動く WebAssembly です。自分で管理している設定だけを使い、`integrity` のハッシュを付けてください。`integrity.model_lib` がないと、ローダーが警告を出します。
- **モデルの返答は信用しないでください。** プレイヤーは入力によって返答を誘導できます（プロンプトインジェクション）。返答でゲームを操作するなら、JSON Schema で形を縛り、ゲームの規則で検証してから実行してください。system プロンプトは、セキュリティの境界にはなりません。JSON Schema は JSON のオブジェクト一つでなければ受け付けないので、リクエストに項目を足すことはできません。
- **Ollama は手元だけで動かしてください。** Ollama には認証がありません。`OLLAMA_HOST=0.0.0.0` や `OLLAMA_ORIGINS=*` で外部に公開しないでください。リモートのホストに暗号化なしの `http` でつなぐと、ライブラリが警告を出します。
- **ログは最小限です。** ライブラリが出すログは、接続先とエラーの種類だけで、会話の内容は出しません。

脆弱性を見つけた場合は、公開の Issue ではなく、GitHub の **Report a vulnerability** から非公開で報告してください。

## ライセンス

MIT です。
WebLLM は Apache-2.0、サンプルのフォント（BIZ UDゴシック）は SIL OFL 1.1 です。詳しくは [Third Party Notices](Packages/com.ryuchan00.local-llm/Third%20Party%20Notices.md) を見てください。
