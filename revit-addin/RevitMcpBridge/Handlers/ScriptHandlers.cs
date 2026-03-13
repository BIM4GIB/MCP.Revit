using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RevitMcpBridge.Handlers
{
    public static class ScriptHandlers
    {
        public static object RunDynamo(Document doc, JObject request)
        {
            string scriptPath = request["scriptPath"]?.Value<string>()
                ?? throw new ArgumentException("scriptPath is required");
            var inputs = request["inputs"] as JObject ?? new JObject();

            if (!File.Exists(scriptPath))
                return ScriptError($"Dynamo script not found: {scriptPath}");

            var sw = Stopwatch.StartNew();

            try
            {
                string sidecarPath = Path.ChangeExtension(scriptPath, ".mcp-inputs.json");
                File.WriteAllText(sidecarPath, inputs.ToString());

                var output = RunDynamoViaApi(doc, scriptPath, inputs);
                sw.Stop();

                return new
                {
                    success         = true,
                    output,
                    errors          = Array.Empty<string>(),
                    executionTimeMs = sw.ElapsedMilliseconds,
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new
                {
                    success         = false,
                    output          = "",
                    errors          = new[] { ex.Message },
                    executionTimeMs = sw.ElapsedMilliseconds,
                };
            }
        }

        private static string RunDynamoViaApi(Document doc, string scriptPath, JObject inputs)
        {
            var dynamoType = Type.GetType(
                "Dynamo.Applications.DynamoRevit, DynamoRevitDS", throwOnError: false);

            if (dynamoType == null)
                throw new InvalidOperationException(
                    "DynamoRevit is not installed or DynamoRevitDS.dll is not loaded. " +
                    "Please install Dynamo for Revit and try again.");

            var runMethod = dynamoType.GetMethod(
                "RunCommandLineJournal",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (runMethod == null)
                throw new InvalidOperationException(
                    "Could not find RunCommandLineJournal on DynamoRevit. " +
                    "Your Dynamo version may not support automation mode.");

            var journalData = new Dictionary<string, string>
            {
                ["dynPath"]        = scriptPath,
                ["dynShowUI"]      = "false",
                ["dynAutomation"]  = "true",
                ["dynPathExecute"] = "true",
            };

            foreach (var kv in inputs)
                journalData[$"dynInput_{kv.Key}"] = kv.Value?.ToString() ?? "";

            runMethod.Invoke(null, new object[] { doc.Application, journalData });
            return $"Dynamo script executed: {Path.GetFileName(scriptPath)}";
        }

        public static object RunPyRevit(Document doc, JObject request)
        {
            string scriptPath = request["scriptPath"]?.Value<string>()
                ?? throw new ArgumentException("scriptPath is required");
            var argsArray = request["args"] as JArray ?? new JArray();

            if (!File.Exists(scriptPath))
                return ScriptError($"pyRevit script not found: {scriptPath}");

            var sw = Stopwatch.StartNew();

            try
            {
                string args = BuildPyRevitArgs(scriptPath, argsArray);
                var (exitCode, stdout, stderr) = RunProcess("pyrevit", args, timeoutMs: 60_000);
                sw.Stop();

                bool success = exitCode == 0;
                return new
                {
                    success,
                    output          = stdout,
                    errors          = success ? Array.Empty<string>() : new[] { stderr },
                    executionTimeMs = sw.ElapsedMilliseconds,
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new
                {
                    success         = false,
                    output          = "",
                    errors          = new[] { ex.Message },
                    executionTimeMs = sw.ElapsedMilliseconds,
                };
            }
        }

        private static string BuildPyRevitArgs(string scriptPath, JArray args)
        {
            var parts = new List<string> { "run", $"\"{scriptPath}\"" };
            foreach (var arg in args)
                parts.Add($"\"{arg}\"");
            return string.Join(" ", parts);
        }

        private static (int exitCode, string stdout, string stderr) RunProcess(
            string exe, string args, int timeoutMs)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName               = exe,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            process.Start();

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();

            bool finished = process.WaitForExit(timeoutMs);
            if (!finished)
            {
                process.Kill();
                throw new TimeoutException(
                    $"Process '{exe}' timed out after {timeoutMs / 1000}s");
            }

            return (process.ExitCode, stdout.Trim(), stderr.Trim());
        }

        private static object ScriptError(string message) => new
        {
            success         = false,
            output          = "",
            errors          = new[] { message },
            executionTimeMs = 0,
        };
    }
}
