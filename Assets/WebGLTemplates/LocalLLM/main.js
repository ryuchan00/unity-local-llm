"use strict";

(function () {
  var canvas = document.getElementById("unity-canvas");
  var loading = document.getElementById("unity-loading");
  var progress = document.getElementById("unity-progress");
  var errorBox = document.getElementById("unity-error");
  var d = canvas.dataset;

  function showError(message) {
    loading.hidden = true;
    errorBox.textContent = message;
    errorBox.hidden = false;
  }

  if (typeof createUnityInstance !== "function") {
    showError("Could not load the Unity loader.");
    return;
  }

  createUnityInstance(canvas, {
    dataUrl: d.data,
    frameworkUrl: d.framework,
    codeUrl: d.code,
    streamingAssetsUrl: "StreamingAssets",
    companyName: d.company,
    productName: d.product,
    productVersion: d.version
  }, function (p) {
    progress.textContent = Math.round(p * 100) + "%";
  }).then(function () {
    loading.hidden = true;
  }).catch(function (e) {
    showError(e && e.message ? e.message : String(e));
  });
})();
