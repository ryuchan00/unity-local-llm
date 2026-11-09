// Loads the bundled WebLLM and exposes it to LocalLLM.jslib as globalThis.LocalLLMWebLLM.
// Loaded as <script type="module"> from the same origin as the game, so the page needs neither
// eval nor inline scripts (CSP: script-src 'self').
import * as webllm from "./web-llm.js";

// Default models pinned to exact commits, with SRI hashes WebLLM verifies after download.
// The URLs in WebLLM's built-in config point at branches that can change; these cannot.
// Regenerate with tools/pin_models.py (repository root) when updating WebLLM.
const MODEL_LIB_BASE =
  "https://raw.githubusercontent.com/mlc-ai/binary-mlc-llm-libs/00fe7a40e57e5e8eb9fef87526a4bf28ad5846c0/web-llm-models/v0_2_86/base/";
// A public file hash, not a secret.
const TOKENIZER_SRI = "sha384-l5K5CpBtbywB5lTHysuesWqCadOP0PqIM5CaLYNPn2xpBA30Q5pAgRo4L1K2EjDM"; // gitleaks:allow

const PINNED_MODELS = {
  "Qwen2.5-1.5B-Instruct-q4f16_1-MLC": {
    model: "https://huggingface.co/mlc-ai/Qwen2.5-1.5B-Instruct-q4f16_1-MLC/resolve/9bd564b064631febf14deadcac492efb761d60c3/",
    model_lib: MODEL_LIB_BASE + "Qwen2-1.5B-Instruct-q4f16_1_cs1k-webgpu.wasm",
    integrity: {
      config: "sha384-edTpAfxnd5O5jvlUbExraVs/Bmqwpni3s8wpFZMcNPSG+5O9sr1ltJJG9zQaM/Vb",
      model_lib: "sha384-fulpsruohkPyMTwf0apV5cWRZWzfOsLlBQjxRbwiNdFg7CZD+iBrRJ5+IL9MenBN",
      tokenizer: { "tokenizer.json": TOKENIZER_SRI },
      onFailure: "error",
    },
  },
  "Qwen2.5-1.5B-Instruct-q4f32_1-MLC": {
    model: "https://huggingface.co/mlc-ai/Qwen2.5-1.5B-Instruct-q4f32_1-MLC/resolve/a822ee410075710c9673005eafa017b90136b85d/",
    model_lib: MODEL_LIB_BASE + "Qwen2-1.5B-Instruct-q4f32_1_cs1k-webgpu.wasm",
    integrity: {
      config: "sha384-s7l0iGYBJ5APr2LPnSwXd3hasAK5nEGN1sn/CuuY34cGyQ6tFkvGxzWmv1x4G3ay",
      model_lib: "sha384-wL+eu+35hvKn1bLSSKiqib+bYqw4/GfY5Y/H7x9SrlkEGeo7AftcjcTKgvly3RuW",
      tokenizer: { "tokenizer.json": TOKENIZER_SRI },
      onFailure: "error",
    },
  },
};

function modelRecord(modelId) {
  const record = webllm.prebuiltAppConfig.model_list.find((m) => m.model_id === modelId);
  if (!record) throw new Error(`Unknown WebLLM model: ${modelId}`);
  const pin = PINNED_MODELS[modelId];
  if (!pin) {
    console.warn(`[LocalLLM] ${modelId} is not pinned; its files are fetched from mutable URLs without integrity checks.`);
    return record;
  }
  return { ...record, ...pin };
}

// customJson: a full WebLLM appConfig (e.g. to self-host the model), or "" for the pinned default.
// A custom config is code-equivalent: its model_lib is WebAssembly that runs in the page.
function buildAppConfig(modelId, customJson) {
  if (!customJson) return { ...webllm.prebuiltAppConfig, model_list: [modelRecord(modelId)] };
  const config = JSON.parse(customJson);
  for (const record of config.model_list || []) {
    if (!record.integrity || !record.integrity.model_lib) {
      console.warn(`[LocalLLM] Custom model ${record.model_id} has no integrity.model_lib; its WebGPU library is not verified.`);
    }
  }
  return config;
}

globalThis.LocalLLMWebLLM = Object.freeze({ webllm, buildAppConfig });
