// HTML text box overlaid on the Unity canvas.
// Unity WebGL text fields cannot receive IME (Japanese) input, so the browser handles typing instead.
var LocalLLMTextInputPlugin = {
  $LocalLLMTextInput: {
    bar: null,
    field: null,
    button: null,
    callback: 0,

    emit: function (text) {
      var size = lengthBytesUTF8(text) + 1;
      var ptr = _malloc(size);
      stringToUTF8(text, ptr, size);
      try {
        {{{ makeDynCall('vi', 'LocalLLMTextInput.callback') }}}(ptr);
      } finally {
        _free(ptr);
      }
    },

    submit: function () {
      var field = LocalLLMTextInput.field;
      var text = field.value.trim();
      if (!text || field.disabled) return;
      field.value = "";
      LocalLLMTextInput.emit(text);
    }
  },

  LocalLLMTextInput_Create: function (placeholderPtr, buttonLabelPtr, maxLength, callback) {
    LocalLLMTextInput.callback = callback;
    if (LocalLLMTextInput.bar) return;

    var canvas = Module["canvas"];
    var parent = canvas.parentElement || document.body;
    if (getComputedStyle(parent).position === "static") parent.style.position = "relative";

    var bar = document.createElement("div");
    bar.style.cssText = "position:absolute;left:16px;right:16px;bottom:16px;display:none;gap:8px;z-index:10;";

    var field = document.createElement("input");
    field.type = "text";
    field.placeholder = UTF8ToString(placeholderPtr);
    field.maxLength = maxLength > 0 ? maxLength : 500;
    field.style.cssText = "flex:1;min-width:0;font-size:18px;padding:8px 10px;border-radius:6px;border:1px solid #888;";

    var button = document.createElement("button");
    button.textContent = UTF8ToString(buttonLabelPtr);
    button.style.cssText = "font-size:16px;padding:8px 18px;border-radius:6px;cursor:pointer;";

    field.addEventListener("keydown", function (e) {
      // isComposing / keyCode 229: Enter that confirms an IME conversion must not send.
      if (e.key === "Enter" && !e.isComposing && e.keyCode !== 229) {
        e.preventDefault();
        LocalLLMTextInput.submit();
      }
      // Keep Unity from treating typing as game input.
      e.stopPropagation();
    });
    field.addEventListener("keyup", function (e) { e.stopPropagation(); });
    field.addEventListener("keypress", function (e) { e.stopPropagation(); });
    button.addEventListener("click", function () { LocalLLMTextInput.submit(); });

    bar.appendChild(field);
    bar.appendChild(button);
    parent.appendChild(bar);

    LocalLLMTextInput.bar = bar;
    LocalLLMTextInput.field = field;
    LocalLLMTextInput.button = button;
  },

  LocalLLMTextInput_SetState: function (visible, enabled) {
    var t = LocalLLMTextInput;
    if (!t.bar) return;
    var wasInactive = t.bar.style.display === "none" || t.field.disabled;
    t.bar.style.display = visible ? "flex" : "none";
    t.field.disabled = !enabled;
    t.button.disabled = !enabled;
    // Return focus to the box when it becomes usable again (e.g. after a reply finished).
    if (visible && enabled && wasInactive) t.field.focus();
  }
};

autoAddDeps(LocalLLMTextInputPlugin, "$LocalLLMTextInput");
mergeInto(LibraryManager.library, LocalLLMTextInputPlugin);
