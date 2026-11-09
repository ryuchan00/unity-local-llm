"""Download a WebLLM release from the npm registry, verify it, and vendor it into the package.

Usage: python tools/vendor_webllm.py 0.2.86

Checks the tarball against the registry's SHA-512 integrity before extracting, then prints the
SHA-256 to record in WebLLMBuildStep.cs and Third Party Notices.md. The registry supplies both the
tarball and its integrity, so also compare the result with the release on GitHub before committing. Uses only the standard library.
"""
import base64
import hashlib
import io
import json
import re
import sys
import tarfile
import urllib.request
from pathlib import Path

PACKAGE = "@mlc-ai/web-llm"
REGISTRY = "https://registry.npmjs.org/"
MAX_FILE_BYTES = 64 * 1024 * 1024
DEST = Path(__file__).resolve().parent.parent / "Packages" / "com.ryuchan00.local-llm" / "WebLLM~"


def read_member(archive: tarfile.TarFile, member: tarfile.TarInfo) -> bytes:
    if member.size > MAX_FILE_BYTES:
        sys.exit(f"{member.name} is unexpectedly large ({member.size} bytes)")
    return archive.extractfile(member).read()


def main(version: str) -> None:
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        sys.exit(f"Not a release version: {version}")
    meta = json.load(urllib.request.urlopen(f"{REGISTRY}{PACKAGE}/{version}", timeout=60))
    tarball, integrity = meta["dist"]["tarball"], meta["dist"]["integrity"]
    if not tarball.startswith(REGISTRY):
        sys.exit(f"Unexpected tarball host: {tarball}")
    data = urllib.request.urlopen(tarball, timeout=300).read()
    actual = "sha512-" + base64.b64encode(hashlib.sha512(data).digest()).decode()
    if actual != integrity:
        sys.exit(f"Integrity mismatch for {tarball}: {actual} != {integrity}")

    with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as archive:
        # Read only the two regular files we need; never extract the archive to disk.
        files = {m.name: m for m in archive.getmembers() if m.isfile()}
        bundle = read_member(archive, files["package/lib/index.js"])
        license_text = read_member(archive, files["package/LICENSE"])

    (DEST / "web-llm.js").write_bytes(bundle)
    (DEST / "web-llm.LICENSE.txt").write_bytes(license_text)
    print(f"Vendored {PACKAGE}@{version}")
    print(f"npm integrity: {integrity}")
    print(f"SHA-256 of web-llm.js: {hashlib.sha256(bundle).hexdigest()}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
