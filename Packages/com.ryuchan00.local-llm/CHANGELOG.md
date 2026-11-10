# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/).

## [0.1.1] - 2026-11-10

### Fixed

- WebLLM failed to load on hosts that serve `.js` files as `application/octet-stream` (such as unityroom), because browsers refuse to run such a file as a module script. The library now fetches the two files, checks their SHA-256, and runs them from `blob:` URLs instead.

## [0.1.0] - 2026-11-09

### Added

- `IChatBackend` with two implementations: `OllamaBackend` (Editor, standalone) and `WebLLMBackend` (WebGL, in the browser via WebGPU).
- `ChatBackendFactory` picks the backend for the current platform from `LocalLLMSettings`.
- Streaming replies, cancellation, and JSON-schema-constrained output.
- `ChatOptions` (temperature, max tokens, JSON schema, thinking) and `ChatResult` with token usage and prompt time.
- `SendAsync` extension returning `Task<ChatResult>`, with `CancellationToken` support.
- `WebTextInput`: an HTML text box for IME input in WebGL builds.
- WebLLM 0.2.86 bundled and copied into WebGL builds, with a SHA-256 check.
- Default WebLLM models pinned to exact commits with SRI hashes.
- `IChatBackend` is `IDisposable`; disposing a `WebLLMBackend` frees the model from GPU memory.
- `ChatErrorKind.Timeout` and a configurable Ollama timeout.
- Chat sample.
