// Bridge between Unity (C#) and WebLLM running in the browser via WebGPU.
// Written in ES5 style so the Emscripten library preprocessor accepts it.
// WebLLM is loaded with a <script type="module"> element pointing at local-llm-loader.js,
// which the build step copies into StreamingAssets. No eval, no inline script, no CDN.
// Some hosts serve .js as application/octet-stream, which browsers refuse to run as a module.
// Only then are the files fetched, checked against the hashes below, and run from blob: URLs instead
// (the build step already checked the same hashes before copying, which covers the direct path).
var LocalLLMPlugin = {
  $LocalLLM: {
    // Keep in sync with WebLLMEvent in WebLLMProtocol.cs.
    PROGRESS: 0,
    READY: 1,
    TOKEN: 2,
    COMPLETE: 3,
    CHAT_ERROR: 4,
    LOAD_ERROR: 5,
    CACHE_STATUS: 6,
    CACHE_DELETED: 7,
    CACHE_ERROR: 8,

    // SHA-256 with CRLF normalized to LF. Keep in sync with WebLLMBuildStep.Sha256 (a test checks).
    SHA256: {
      "web-llm.js": "4c9772f6973eb03b2499c2128a71a2f9cfb542321dc4ef01a66de4a9f376c66f",
      "local-llm-loader.js": "0f6819e21b041f68e62acb6c81caaa5ca75eecf2513f99f9a21687e45c6cee97"
    },
    WEBLLM_IMPORT: "\"./web-llm.js\"",

    callback: 0,
    baseUrl: "",
    appConfigJson: "",
    engine: null,
    modulePromise: null,

    emit: function (kind, text) {
      if (!LocalLLM.callback) return;
      var s = text == null ? "" : String(text);
      var size = lengthBytesUTF8(s) + 1;
      var ptr = _malloc(size);
      stringToUTF8(s, ptr, size);
      try {
        {{{ makeDynCall('vii', 'LocalLLM.callback') }}}(kind, ptr);
      } finally {
        _free(ptr);
      }
    },

    errorText: function (e) {
      return e && e.message ? e.message : String(e);
    },

    loadModule: function () {
      if (LocalLLM.modulePromise) return LocalLLM.modulePromise;
      var url = new URL("local-llm-loader.js", LocalLLM.baseUrl).href;
      var promise = globalThis.LocalLLMWebLLM
        ? Promise.resolve(globalThis.LocalLLMWebLLM)
        : LocalLLM.runModule(url).catch(function (direct) {
            // A loader that ran but did not initialize is not retried; running it twice could double-initialize.
            if (!direct.loadFailed) throw direct;
            return LocalLLM.loadFromBlobs().catch(function (fallback) {
              throw new Error("Could not load " + url + " (" + LocalLLM.errorText(direct) + "; then " +
                LocalLLM.errorText(fallback) + "). Was the WebGL build made with the package's build step? " +
                "If the page has a Content Security Policy, its script-src needs blob: for hosts that serve .js as octet-stream.");
            });
          });
      // Allow a retry after a failed load.
      promise.catch(function () { LocalLLM.modulePromise = null; });
      LocalLLM.modulePromise = promise;
      return promise;
    },

    runModule: function (src) {
      return new Promise(function (resolve, reject) {
        var script = document.createElement("script");
        script.type = "module";
        script.src = src;
        // A module script fires load after it has run.
        script.onload = function () {
          script.remove();
          if (globalThis.LocalLLMWebLLM) resolve(globalThis.LocalLLMWebLLM);
          else reject(new Error("local-llm-loader.js did not initialize"));
        };
        script.onerror = function () {
          script.remove();
          var error = new Error("the module script failed to load");
          error.loadFailed = true;
          reject(error);
        };
        document.head.appendChild(script);
      });
    },

    // Runs the same two files from blob: URLs, after checking their hashes, so the host's MIME type does not matter.
    loadFromBlobs: function () {
      return Promise.all([LocalLLM.fetchVerified("local-llm-loader.js"), LocalLLM.fetchVerified("web-llm.js")])
        .then(function (texts) {
          if (texts[0].indexOf(LocalLLM.WEBLLM_IMPORT) < 0) throw new Error("local-llm-loader.js does not import web-llm.js");
          var libUrl = LocalLLM.blobUrl(texts[1]);
          var loaderUrl = LocalLLM.blobUrl(texts[0].split(LocalLLM.WEBLLM_IMPORT).join(JSON.stringify(libUrl)));
          var revoke = function () {
            URL.revokeObjectURL(loaderUrl);
            URL.revokeObjectURL(libUrl);
          };
          return LocalLLM.runModule(loaderUrl).then(
            function (lib) { revoke(); return lib; },
            function (e) { revoke(); throw e; });
        });
    },

    blobUrl: function (text) {
      return URL.createObjectURL(new Blob([text], { type: "text/javascript" }));
    },

    // Resolves to the file's text with CRLF normalized to LF, or rejects if its hash is not the recorded one.
    fetchVerified: function (file) {
      if (!globalThis.crypto || !crypto.subtle) {
        return Promise.reject(new Error("checking " + file + " needs a secure context (https)"));
      }
      // no-cache: a copy cached before a package update would fail the hash check.
      return fetch(new URL(file, LocalLLM.baseUrl).href, { credentials: "same-origin", cache: "no-cache" })
        .then(function (response) {
          if (!response.ok) throw new Error(file + ": HTTP " + response.status);
          return response.arrayBuffer();
        })
        .then(function (buffer) {
          var text = new TextDecoder("utf-8", { fatal: true, ignoreBOM: true }).decode(buffer).replace(/\r\n/g, "\n");
          return crypto.subtle.digest("SHA-256", new TextEncoder().encode(text)).then(function (digest) {
            var hex = Array.prototype.map.call(new Uint8Array(digest), function (b) {
              return ("0" + b.toString(16)).slice(-2);
            }).join("");
            if (hex !== LocalLLM.SHA256[file]) throw new Error(file + " does not match the bundled file");
            return text;
          });
        });
    },

    // q4f16 models need the shader-f16 GPU feature; fall back to the f32 variant without it.
    resolveModelId: function (modelId) {
      if (typeof navigator === "undefined" || !navigator.gpu) {
        return Promise.reject(new Error("This browser does not support WebGPU."));
      }
      return navigator.gpu.requestAdapter().then(function (adapter) {
        if (!adapter) {
          throw new Error("No WebGPU adapter. Enable hardware acceleration in the browser settings.");
        }
        return adapter.features.has("shader-f16") ? modelId : modelId.replace("q4f16_1", "q4f32_1");
      });
    },

    hasModel: function (appConfig, modelId) {
      return (appConfig.model_list || []).some(function (m) { return m.model_id === modelId; });
    },

    withModel: function (modelId, action) {
      return Promise.all([LocalLLM.loadModule(), LocalLLM.resolveModelId(modelId)]).then(function (r) {
        var lib = r[0];
        var resolved = r[1];
        var appConfig = lib.buildAppConfig(resolved, LocalLLM.appConfigJson);
        // A custom appConfig may not list the f32 variant; keep the requested model then.
        if (resolved !== modelId && !LocalLLM.hasModel(appConfig, resolved)) {
          resolved = modelId;
          appConfig = lib.buildAppConfig(resolved, LocalLLM.appConfigJson);
        }
        return action(lib, resolved, appConfig);
      });
    }
  },

  LocalLLM_IsWebGPUSupported: function () {
    return typeof navigator !== "undefined" && navigator.gpu ? 1 : 0;
  },

  LocalLLM_Setup: function (baseUrlPtr, appConfigPtr, callback) {
    LocalLLM.baseUrl = UTF8ToString(baseUrlPtr);
    LocalLLM.appConfigJson = UTF8ToString(appConfigPtr);
    LocalLLM.callback = callback;
  },

  LocalLLM_Init: function (modelPtr) {
    var modelId = UTF8ToString(modelPtr);
    LocalLLM.emit(LocalLLM.PROGRESS, "Loading WebLLM...");
    LocalLLM.withModel(modelId, function (lib, resolved, appConfig) {
      return lib.webllm.CreateMLCEngine(resolved, {
        appConfig: appConfig,
        initProgressCallback: function (report) {
          LocalLLM.emit(LocalLLM.PROGRESS, report.text);
        }
      }).then(function (engine) {
        LocalLLM.engine = engine;
        LocalLLM.emit(LocalLLM.READY, resolved);
      });
    }).catch(function (e) {
      console.error("[LocalLLM] init failed", e);
      LocalLLM.emit(LocalLLM.LOAD_ERROR, LocalLLM.errorText(e));
    });
  },

  LocalLLM_Chat: function (requestJsonPtr) {
    var engine = LocalLLM.engine;
    if (!engine) {
      LocalLLM.emit(LocalLLM.CHAT_ERROR, "Engine is not loaded.");
      return;
    }

    var request;
    try {
      request = JSON.parse(UTF8ToString(requestJsonPtr));
    } catch (e) {
      LocalLLM.emit(LocalLLM.CHAT_ERROR, "Invalid request: " + LocalLLM.errorText(e));
      return;
    }

    var params = {
      messages: request.messages,
      stream: true,
      stream_options: { include_usage: true }
    };
    if (typeof request.temperature === "number") params.temperature = request.temperature;
    if (typeof request.max_tokens === "number") params.max_tokens = request.max_tokens;
    if (request.json_schema) params.response_format = { type: "json_object", schema: request.json_schema };

    var reply = "";
    var usage = null;
    engine.chat.completions.create(params)
      .then(function (stream) {
        var iterator = stream[Symbol.asyncIterator]();
        function step() {
          return iterator.next().then(function (result) {
            if (result.done) return;
            var chunk = result.value;
            if (chunk.usage) usage = chunk.usage;
            var choice = chunk.choices && chunk.choices[0];
            var delta = choice && choice.delta && choice.delta.content;
            if (delta) {
              reply += delta;
              LocalLLM.emit(LocalLLM.TOKEN, delta);
            }
            return step();
          });
        }
        return step();
      })
      .then(function () {
        LocalLLM.emit(LocalLLM.COMPLETE, JSON.stringify({
          text: reply,
          has_usage: !!usage,
          prompt_tokens: usage ? usage.prompt_tokens : 0,
          completion_tokens: usage ? usage.completion_tokens : 0,
          time_to_first_token_s: usage && usage.extra ? usage.extra.time_to_first_token_s : 0
        }));
      })
      .catch(function (e) {
        console.error("[LocalLLM] chat failed", e);
        try {
          engine.interruptGenerate();
        } catch (ignored) {
          // Already stopped.
        }
        LocalLLM.emit(LocalLLM.CHAT_ERROR, LocalLLM.errorText(e));
      });
  },

  LocalLLM_Cancel: function () {
    if (LocalLLM.engine) LocalLLM.engine.interruptGenerate();
  },

  LocalLLM_Unload: function () {
    var engine = LocalLLM.engine;
    LocalLLM.engine = null;
    LocalLLM.callback = 0;
    if (engine) {
      engine.unload().catch(function (e) {
        console.warn("[LocalLLM] unload failed", e);
      });
    }
  },

  LocalLLM_CheckCache: function (modelPtr) {
    var modelId = UTF8ToString(modelPtr);
    LocalLLM.withModel(modelId, function (lib, resolved, appConfig) {
      return lib.webllm.hasModelInCache(resolved, appConfig);
    }).then(function (cached) {
      LocalLLM.emit(LocalLLM.CACHE_STATUS, cached ? "true" : "false");
    }).catch(function (e) {
      LocalLLM.emit(LocalLLM.CACHE_ERROR, LocalLLM.errorText(e));
    });
  },

  LocalLLM_DeleteCache: function (modelPtr) {
    var modelId = UTF8ToString(modelPtr);
    LocalLLM.withModel(modelId, function (lib, resolved, appConfig) {
      return lib.webllm.deleteModelAllInfoInCache(resolved, appConfig);
    }).then(function () {
      LocalLLM.emit(LocalLLM.CACHE_DELETED, "");
    }).catch(function (e) {
      LocalLLM.emit(LocalLLM.CACHE_ERROR, LocalLLM.errorText(e));
    });
  }
};

autoAddDeps(LocalLLMPlugin, "$LocalLLM");
mergeInto(LibraryManager.library, LocalLLMPlugin);
