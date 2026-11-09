# Security Policy

## Reporting a vulnerability

Please report vulnerabilities privately through GitHub's **Report a vulnerability** button on the Security tab of this repository. Do not open a public issue.

## Supported versions

Only the latest release receives fixes.

## How third-party code is handled

- WebLLM is vendored from the npm registry after checking the registry's SHA-512 integrity, and WebGL builds refuse to copy it if its SHA-256 has changed. See `tools/vendor_webllm.py`.
- Default WebLLM model files are requested from fixed commits and checked with SRI hashes. See `tools/pin_models.py`.
- GitHub Actions are pinned to commit SHAs.
