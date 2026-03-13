using Autodesk.Revit.UI;
using System;
using System.Threading;

namespace RevitMcpBridge
{
    /// <summary>
    /// Marshals work from the HTTP thread onto Revit's main thread via ExternalEvent.
    /// Uses a SemaphoreSlim to prevent concurrent requests from racing.
    /// </summary>
    public class RevitEventHandler : IExternalEventHandler
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Action<UIDocument>? _work;

        /// <summary>
        /// Queue work to run on Revit's main thread.
        /// Blocks if a previous request is still pending.
        /// </summary>
        public void SetWork(Action<UIDocument> work)
        {
            _gate.Wait();
            _work = work;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (_work == null) return;
                var work = _work;
                _work = null;

                var uiDoc = app.ActiveUIDocument;
                if (uiDoc == null)
                {
                    throw new InvalidOperationException(
                        "No active document. Please open a Revit model first.");
                }

                work(uiDoc);
            }
            finally
            {
                _gate.Release();
            }
        }

        public string GetName() => "RevitMcpBridgeEvent";
    }
}
