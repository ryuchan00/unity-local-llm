"""Print pinned URLs and SRI hashes for the default WebLLM models, for local-llm-loader.js.

Usage: python tools/pin_models.py v0_2_86/base

Resolves the current commit of each Hugging Face model repository and of the WebGPU library
file, downloads the config, tokenizer and library at those commits, and hashes them (SHA-384).
Uses only the standard library. Review the output before pasting it into the loader.
"""
import base64
import hashlib
import json
import re
import sys
import urllib.parse
import urllib.request

MODELS = {
    "Qwen2.5-1.5B-Instruct-q4f16_1-MLC": "Qwen2-1.5B-Instruct-q4f16_1_cs1k-webgpu.wasm",
    "Qwen2.5-1.5B-Instruct-q4f32_1-MLC": "Qwen2-1.5B-Instruct-q4f32_1_cs1k-webgpu.wasm",
}
LIB_REPO = "mlc-ai/binary-mlc-llm-libs"


def get(url: str) -> bytes:
    return urllib.request.urlopen(url, timeout=300).read()


def sri(data: bytes) -> str:
    return "sha384-" + base64.b64encode(hashlib.sha384(data).digest()).decode()


def main(model_version: str) -> None:
    if not re.fullmatch(r"v\d+_\d+_\d+/base", model_version):
        sys.exit(f"Expected a model library version like v0_2_86/base, got: {model_version}")
    for model_id, lib_file in MODELS.items():
        hf_sha = json.loads(get(f"https://huggingface.co/api/models/mlc-ai/{model_id}"))["sha"]
        lib_path = f"web-llm-models/{model_version}/{lib_file}"
        commits = json.loads(get(
            f"https://api.github.com/repos/{LIB_REPO}/commits?per_page=1&path={urllib.parse.quote(lib_path)}"))
        lib_sha = commits[0]["sha"]

        model_url = f"https://huggingface.co/mlc-ai/{model_id}/resolve/{hf_sha}/"
        lib_url = f"https://raw.githubusercontent.com/{LIB_REPO}/{lib_sha}/{lib_path}"
        print(json.dumps({model_id: {
            "model": model_url,
            "model_lib": lib_url,
            "integrity": {
                "config": sri(get(model_url + "mlc-chat-config.json")),
                "model_lib": sri(get(lib_url)),
                "tokenizer": {"tokenizer.json": sri(get(model_url + "tokenizer.json"))},
                "onFailure": "error",
            },
        }}, indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
