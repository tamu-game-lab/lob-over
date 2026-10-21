"""WO-S6 source acceptance scan; requires only the installed Python standard library."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
files = [*root.joinpath("Editor").rglob("*.cs"), *root.joinpath("Runtime").rglob("*.cs")]
errors = []
for path in files:
    source = path.read_text(encoding="utf-8-sig")
    if re.search(r"AddComponent\s*<\s*OVR", source):
        errors.append(f"{path}: adds an OVR component")
    if re.search(r"Instantiate\s*\([^;]*(?:OVRCameraRig|rig\.gameObject)", source):
        errors.append(f"{path}: copies a camera rig")
    if "OVRInput." in source and "Physics.Raycast" in source:
        errors.append(f"{path}: controller input and manual raycast coexist")
    if re.search(r"\b(?:rig|CameraRig)\.transform\.(?:SetParent|RotateAround|position\s*=)", source):
        errors.append(f"{path}: moves or reparents the rig")
if root.joinpath("Runtime/VR/ControllerPointer.cs").exists():
    errors.append("The custom ControllerPointer remains")
if errors:
    raise SystemExit("\n".join(errors))
print(f"PASS: {len(files)} source files; no OVR component creation, rig copying/reparenting, or OVRInput raycasts; custom pointer removed.")
