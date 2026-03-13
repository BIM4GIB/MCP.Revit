using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;

namespace RevitMcpBridge
{
    [Regeneration(RegenerationOption.Manual)]
    public class App : IExternalApplication
    {
        private BridgeServer? _server;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                int port = BridgeConfig.Port;
                _server = new BridgeServer(port, application);
                _server.Start();

                Logger.Info($"Bridge started on http://localhost:{port}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start bridge: {ex.Message}");
                TaskDialog.Show("Revit MCP Bridge",
                    $"Failed to start bridge server:\n{ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            _server?.Stop();
            Logger.Info("Bridge stopped");
            return Result.Succeeded;
        }
    }
}
