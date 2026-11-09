# Third Party Notices

This package includes the following third-party software.

## WebLLM

- Files: `WebLLM~/web-llm.js`, license in `WebLLM~/web-llm.LICENSE.txt`
- Source: `lib/index.js` of the npm package `@mlc-ai/web-llm` 0.2.86 (https://github.com/mlc-ai/web-llm)
- License: Apache License 2.0
- Copyright: the MLC LLM and WebLLM contributors
- Retrieved from: https://registry.npmjs.org/@mlc-ai/web-llm/-/web-llm-0.2.86.tgz
- npm integrity of the tarball (verified before extraction): `sha512-plciXBCYtY7iTKHzEcsqOfSO+s5u8NqMYFPnHmRbsN8CEdKcolgXIoGLPbzO6FtJHV43m7YP00x6qQ6Kb8CJJw==`
- SHA-256 of `web-llm.js` (checked on every WebGL build): `ae2642e73a4a9896c21efb3a067c4666446f8b49cbe2012f4b576f5d83d5f206`

The file is unmodified. It bundles its dependency `loglevel` (MIT License, Copyright (c) 2013 Tim Perry).

## BIZ UDGothic (Chat sample only)

- Files: `Samples~/Chat/Fonts/BIZUDGothic-Regular.ttf`, license in `Samples~/Chat/Fonts/OFL.txt`
- Source: https://github.com/googlefonts/morisawa-biz-ud-gothic
- License: SIL Open Font License 1.1
- Copyright 2022 The BIZ UDGothic Project Authors

## Models

The package does not include model weights. At runtime, WebLLM downloads the model chosen in `LocalLLMSettings` (by default Qwen2.5-1.5B-Instruct, Apache License 2.0, converted by the MLC team) from Hugging Face, and its WebGPU library from https://github.com/mlc-ai/binary-mlc-llm-libs. Check the license of any model you ship with your game.
