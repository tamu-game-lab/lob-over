using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace LOBOver.Editor
{
    public static class QuestBuild
    {
        // WO-S5 Amendment 1 sets the install and other adb deadlines.
        const int InstallTimeoutMs = 120000;
        const int CommandTimeoutMs = 30000;
        // Amendment 1 identifies this Quest; unauthorized adb rows can omit their model.
        const string KnownQuestSerial = "340YC10GBC0BRS";
        const string NoQuest = "no Quest connected (developer mode on, cable in, allow USB debugging in the headset)";

        [MenuItem("LOB Over/Build/Build Quest APK")]
        public static void BuildQuestApk() => Build(false);

        [MenuItem("LOB Over/Build/Build and Run Quest")]
        public static void BuildAndRunQuest() => Build(true);

        static void Build(bool run)
        {
            try
            {
                var passes = BuildChecks.Passes(out var settingsReport);
                if (!passes) Debug.LogError("LOB Over: " + settingsReport);
                if (!File.Exists(SiteSceneBuilder.ScenePath))
                {
                    Debug.LogError("LOB Over: run LOB Over/Build/Site Scene first");
                    return;
                }
                // Missing() includes the active target itself; switching must still be possible.
                // A switch always returns without building, so all checks run again after reimport.
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    var switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                    if (switched) Debug.Log("LOB Over: switched to Android; re-run the build menu item after reimport.");
                    else Debug.LogError("LOB Over: Android build target switch failed.");
                    return;
                }
                if (!passes) return;

                var root = Directory.GetParent(Application.dataPath).FullName;
                string adb = null;
                var serial = run ? FindQuest(root, out adb) : null;
                var directory = Path.Combine(root, "Builds");
                Directory.CreateDirectory(directory);
                var output = Path.Combine(directory, "LOBOver-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".apk");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    locationPathName = output,
                    target = BuildTarget.Android,
                    // Never let Unity choose between the Quest and an attached phone (Amendment 1).
                    options = BuildOptions.None
                });
                WriteLastBuild(root, report.summary.outputPath);

                string deployment = "";
                if (report.summary.result == BuildResult.Succeeded && run)
                    deployment = serial == null ? "; APK only (no authorized Quest)" : "; " + InstallAndStart(adb, serial, output, root);
                var summary = report.summary;
                Debug.Log("LOB Over: " + summary.result + "; size=" + summary.totalSize + " bytes; time=" + summary.totalTime
                    + "; output=" + OneLine(summary.outputPath) + deployment);
                if (summary.result != BuildResult.Succeeded) LogFirstError(report);
            }
            catch (Exception exception)
            {
                Debug.LogError("LOB Over: Quest build failed: " + OneLine(exception.GetBaseException().Message));
            }
        }

        static string FindQuest(string root, out string adb)
        {
            adb = null;
            try
            {
                // The Android extension is optional and absent from the offline compiler's references.
                var settings = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions", true);
                var sdk = (string)settings.GetProperty("sdkRootPath").GetValue(null);
                adb = Path.Combine(sdk, "platform-tools", Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("LOB Over: adb devices -l failed: " + OneLine(exception.GetBaseException().Message));
                Debug.LogWarning("LOB Over: " + NoQuest);
                return null;
            }

            var result = RunProcess(adb, new[] { "devices", "-l" }, root, CommandTimeoutMs);
            if (!result.Succeeded)
            {
                LogProcess("devices -l", "none", result);
                Debug.LogWarning("LOB Over: " + NoQuest);
                return null;
            }
            var serial = SelectQuest(result.Stdout, out var unauthorized);
            if (serial == null)
                Debug.LogWarning("LOB Over: " + (unauthorized
                    ? "Quest connected but not authorized: put the headset on and allow USB debugging"
                    : NoQuest));
            return serial;
        }

        static string SelectQuest(string devices, out bool unauthorized)
        {
            unauthorized = false;
            foreach (var line in devices.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 2) continue;
                var questModel = fields.Skip(2).Any(field => field.StartsWith("model:", StringComparison.Ordinal)
                    && field.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0);
                if (fields[1] == "unauthorized" && (questModel || fields[0] == KnownQuestSerial)) unauthorized = true;
                if (!questModel) continue;
                if (fields[1] == "device") return fields[0];
            }
            return null;
        }

        static string InstallAndStart(string adb, string serial, string apk, string root)
        {
            var install = RunProcess(adb, new[] { "-s", serial, "install", "-r", apk }, root, InstallTimeoutMs);
            LogProcess("install", serial, install);
            if (!install.Succeeded) return "install failed on " + serial;
            var start = RunProcess(adb, new[] { "-s", serial, "shell", "monkey", "-p", PlayerSettings.applicationIdentifier,
                "-c", "android.intent.category.LAUNCHER", "1" }, root, CommandTimeoutMs);
            LogProcess("start", serial, start);
            return start.Succeeded ? "installed and started on " + serial : "start failed on " + serial;
        }

        static void WriteLastBuild(string root, string output)
        {
            var hash = RunProcess("git", new[] { "rev-parse", "--short", "HEAD" }, root, CommandTimeoutMs);
            if (!hash.Succeeded) LogProcess("git rev-parse --short HEAD", "none", hash);
            try
            {
                File.WriteAllText(Path.Combine(root, "Builds", "last-build.txt"), "output=" + output
                    + Environment.NewLine + "git=" + (hash.Succeeded ? hash.Stdout.Trim() : "unavailable")
                    + Environment.NewLine + "time=" + DateTimeOffset.Now.ToString("o") + Environment.NewLine);
            }
            catch (Exception exception)
            {
                Debug.LogError("LOB Over: last-build.txt failed: " + OneLine(exception.Message));
            }
        }

        static void LogFirstError(BuildReport report)
        {
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                    {
                        Debug.LogError("LOB Over: first error step " + OneLine(step.name) + ": " + OneLine(message.content));
                        return;
                    }
            Debug.LogError("LOB Over: build " + report.summary.result + "; no error step was reported.");
        }

        sealed class ProcessResult
        {
            public int ExitCode = -1;
            public bool TimedOut;
            public string Stdout = "";
            public string Stderr = "";
            public bool Succeeded => ExitCode == 0 && !TimedOut;
        }

        static ProcessResult RunProcess(string executable, string[] arguments, string root, int timeoutMs)
        {
            var result = new ProcessResult();
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = string.Join(" ", arguments.Select(QuoteArgument)),
                        WorkingDirectory = root,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    process.Start();
                    var timer = Stopwatch.StartNew();
                    var stdoutTask = CaptureOutput(process.StandardOutput, stdout);
                    var stderrTask = CaptureOutput(process.StandardError, stderr);
                    var exited = process.WaitForExit(timeoutMs);
                    var remaining = (int)Math.Max(0, timeoutMs - timer.ElapsedMilliseconds);
                    result.TimedOut = !exited || !Task.WaitAll(new[] { stdoutTask, stderrTask }, remaining);
                    if (result.TimedOut && !process.HasExited)
                    {
                        process.Kill();
                        // Bound cleanup too; never hang the Editor waiting for a failed adb call.
                        process.WaitForExit(1000);
                    }
                    result.ExitCode = exited ? process.ExitCode : -1;
                }
            }
            catch (Exception exception)
            {
                result.ExitCode = -1;
                result.Stderr += " " + exception.Message;
            }
            lock (stdout) result.Stdout = stdout.ToString();
            lock (stderr) result.Stderr = stderr + result.Stderr;
            return result;
        }

        static async Task CaptureOutput(StreamReader reader, StringBuilder output)
        {
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                lock (output) output.Append(buffer, 0, count);
        }

        static void LogProcess(string step, string serial, ProcessResult result)
        {
            var line = "LOB Over: " + step + "; serial=" + serial + "; exit code=" + result.ExitCode;
            if (result.Succeeded) Debug.Log(line);
            else Debug.LogError(line + (result.TimedOut ? "; timed out" : "")
                + "; stdout=" + OneLine(result.Stdout) + "; stderr=" + OneLine(result.Stderr));
        }

        static string OneLine(string value) => string.Join(" ", (value ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

        static string QuoteArgument(string value)
        {
            // Windows process arguments require escaped quotes and doubled trailing backslashes.
            var quoted = new StringBuilder("\"");
            var slashes = 0;
            foreach (var character in value)
            {
                if (character == '\\') { slashes++; continue; }
                quoted.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                quoted.Append(character);
                slashes = 0;
            }
            return quoted.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
