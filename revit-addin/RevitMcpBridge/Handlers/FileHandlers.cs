using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace RevitMcpBridge.Handlers
{
    public static class FileHandlers
    {
        public static object SaveAs(Document doc, JObject request)
        {
            string newName = request["newName"]?.Value<string>()
                ?? throw new ArgumentException("newName is required");
            string targetFolder = request["targetFolder"]?.Value<string>()
                ?? throw new ArgumentException("targetFolder is required");
            bool overwrite     = request["overwrite"]?.Value<bool>() ?? false;
            bool compact       = request["compact"]?.Value<bool>() ?? true;
            string wsMode      = request["worksharingMode"]?.Value<string>() ?? "detach";

            string originalPath = doc.PathName;

            if (!newName.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
                newName += ".rvt";

            Directory.CreateDirectory(targetFolder);
            string newPath = Path.Combine(targetFolder, newName);

            if (File.Exists(newPath) && !overwrite)
                return new
                {
                    success          = false,
                    newFilePath      = newPath,
                    originalFilePath = originalPath,
                    message          = $"File already exists: {newPath}. Set overwrite=true to replace it.",
                };

            var saveOptions = new SaveAsOptions
            {
                OverwriteExistingFile = overwrite,
                Compact               = compact,
            };

            if (doc.IsWorkshared)
            {
                switch (wsMode.ToLowerInvariant())
                {
                    case "detach":
                        saveOptions.SetWorksharingOptions(
                            new WorksharingSaveAsOptions { SaveAsCentral = false });
                        break;
                    case "preserve":
                        saveOptions.SetWorksharingOptions(
                            new WorksharingSaveAsOptions { SaveAsCentral = true });
                        break;
                }
            }

            doc.SaveAs(newPath, saveOptions);

            return new
            {
                success          = true,
                newFilePath      = newPath,
                originalFilePath = originalPath,
                message          = $"Saved to {newPath}",
            };
        }

        public static object OpenAndUpgrade(Document doc, JObject request)
        {
            string sourcePath = request["sourcePath"]?.Value<string>()
                ?? throw new ArgumentException("sourcePath is required");
            bool audit = request["audit"]?.Value<bool>() ?? true;

            if (!File.Exists(sourcePath))
                return new
                {
                    success      = false,
                    message      = $"File not found: {sourcePath}",
                    upgradedPath = "",
                };

            try
            {
                var openOptions = new OpenOptions
                {
                    Audit = audit,
                    DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets,
                };

                var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(sourcePath);
                var app = doc.Application;
                var uiApp = new Autodesk.Revit.UI.UIApplication(app);
                var openedDoc = uiApp.OpenAndActivateDocument(modelPath, openOptions, false);

                return new
                {
                    success      = true,
                    message      = $"Opened and upgraded: {sourcePath}\nRevit version: {app.VersionNumber}",
                    upgradedPath = openedDoc.Document.PathName,
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success      = false,
                    message      = $"Failed to open: {ex.Message}",
                    upgradedPath = "",
                };
            }
        }
    }
}
