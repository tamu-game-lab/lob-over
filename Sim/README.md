# LOBOver.Sim
Run commands from the repository root with the installed .NET SDK.
Build: dotnet build Sim/LOBOver.Sim.csproj -c Release
Test: dotnet test Sim/LOBOver.Sim.Tests -c Release
NuGet.Config disables network sources; restore uses already cached package versions.
In the dispatch sandbox set $env:NUGET_PACKAGES = 'C:\Users\bryan\.nuget\packages'.
The netstandard2.1 project links the C# 9 files in Assets/LOBOver/Sim.
Unity compiles those same files via LOBOver.Sim.asmdef, without engine references.
InspectAt(target, stationFt) is a read-only presenter-only MR diorama query; it has no seat, message, state or deviation effects.
The asmdef references Newtonsoft.Json.dll from com.unity.nuget.newtonsoft-json 3.2.1.
Pacer.Advance takes elapsed milliseconds; Sync after commands; Autoplan skips Site.
Regenerate RNG: node Sim/LOBOver.Sim.Tests/fixtures/generate-rng.mjs
After JS changes the orchestrator regenerates all six transcripts with node Monitor/src/sim/headless.js Data/Scenarios/<stage>.json --script Monitor/test/scripts/<name>.json --out Data/transcripts/<name>.json (omit --script for stage1_noaction; see Docs/ORCHESTRATOR_SHOWCASE.md).
