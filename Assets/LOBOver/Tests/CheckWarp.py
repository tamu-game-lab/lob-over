"""WO-L5 source acceptance scan, runnable without Unity or additional dependencies."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
failures = []
paths = sorted([*root.rglob("*.cs"), *root.rglob("*.shader")])
# WO-L5 Amendment 1: only the legacy Site.unity inspector mask is exempt.
# The amendment names Art/ComfortFade.shader; its tracked location is Runtime/VR.
legacy_mask = {"Runtime/VR/ComfortFade.cs", "Runtime/VR/ComfortFade.shader"}
sources = [p for p in paths if "Tests" not in p.relative_to(root).parts
           and p.relative_to(root).as_posix() not in legacy_mask]
for path in sources:
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        if re.search(r"ZTest\s+Always|WarpSphere|fullMesh|RideVignette", line):
            failures.append(f"{path.relative_to(root)}:{number}: {line.strip()}")
for name in ("Warp", "WarpFade"):
    for extension in (".shader", ".shader.meta", ".mat", ".mat.meta"):
        if (root / "Art" / (name + extension)).exists():
            failures.append(f"Removed asset remains: Art/{name}{extension}")
if failures:
    raise SystemExit("FAIL: WO-L5 source scan\n" + "\n".join(failures))
print(f"PASS: {len(sources)} source files; no always-depth-test or view-covering mesh; old warp assets absent.")
