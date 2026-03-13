using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMcpBridge.Handlers;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitMcpBridge
{
    public class BridgeServer
    {
        private readonly HttpListener _listener;
        private readonly RevitEventHandler _eventHandler;
        private readonly ExternalEvent _externalEvent;
        private CancellationTokenSource? _cts;
        private Thread? _listenerThread;

        public BridgeServer(int port, UIControlledApplication uiApp)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");

            _eventHandler = new RevitEventHandler();
            _externalEvent = ExternalEvent.Create(_eventHandler);
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();

            _listenerThread = new Thread(() => ListenLoop(_cts.Token))
            {
                IsBackground = true
            };
            _listenerThread.Start();
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener.Stop();
        }

        private void ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = _listener.GetContext();
                    Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Listener error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            try
            {
                string body = "";
                if (req.HasEntityBody)
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    body = await reader.ReadToEndAsync();
                }

                JObject? requestData = string.IsNullOrEmpty(body)
                    ? null
                    : JObject.Parse(body);

                var (statusCode, responseObj) = await RouteRequest(
                    req.HttpMethod,
                    req.Url!.AbsolutePath.TrimEnd('/'),
                    requestData);

                string json = JsonConvert.SerializeObject(responseObj);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                res.StatusCode = statusCode;
                res.ContentType = "application/json";
                res.ContentLength64 = buffer.Length;
                await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Logger.Error($"Request error: {ex.Message}");
                byte[] err = Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new { message = ex.Message }));
                res.StatusCode = 500;
                res.ContentType = "application/json";
                res.ContentLength64 = err.Length;
                await res.OutputStream.WriteAsync(err, 0, err.Length);
            }
            finally
            {
                res.OutputStream.Close();
            }
        }

        private async Task<(int statusCode, object response)> RouteRequest(
            string method, string path, JObject? body)
        {
            // GET /ping
            if (method == "GET" && path == "/ping")
                return (200, new { status = "ok", version = "1.0" });

            // GET /model/info
            if (method == "GET" && path == "/model/info")
                return (200, (await RunOnRevitThread(doc => ModelHandlers.GetModelInfo(doc)))!);

            // GET /model/categories
            if (method == "GET" && path == "/model/categories")
                return (200, (await RunOnRevitThread(doc => ModelHandlers.GetCategories(doc)))!);

            // GET /model/levels
            if (method == "GET" && path == "/model/levels")
                return (200, (await RunOnRevitThread(doc => ModelHandlers.GetLevels(doc)))!);

            // POST /elements/query
            if (method == "POST" && path == "/elements/query")
                return (200, (await RunOnRevitThread(doc => ElementHandlers.QueryElements(doc, body!)))!);

            // GET /elements/{id}  (64-bit element IDs since Revit 2024+)
            if (method == "GET" && path.StartsWith("/elements/"))
            {
                var idStr = path["/elements/".Length..];
                if (long.TryParse(idStr, out long id))
                {
                    var result = await RunOnRevitThread(doc => ElementHandlers.GetElementById(doc, id));
                    return result != null
                        ? (200, result)
                        : (404, new { message = $"Element {id} not found" });
                }
                return (400, new { message = "Invalid element ID" });
            }

            // POST /scripts/dynamo
            if (method == "POST" && path == "/scripts/dynamo")
                return (200, (await RunOnRevitThread(doc => ScriptHandlers.RunDynamo(doc, body!)))!);

            // POST /scripts/pyrevit
            if (method == "POST" && path == "/scripts/pyrevit")
                return (200, (await RunOnRevitThread(doc => ScriptHandlers.RunPyRevit(doc, body!)))!);

            // POST /file/saveas
            if (method == "POST" && path == "/file/saveas")
                return (200, (await RunOnRevitThread(doc => FileHandlers.SaveAs(doc, body!)))!);

            // POST /file/open-upgrade
            if (method == "POST" && path == "/file/open-upgrade")
                return (200, (await RunOnRevitThread(doc => FileHandlers.OpenAndUpgrade(doc, body!)))!);

            return (404, new { message = $"No route for {method} {path}" });
        }

        private Task<T?> RunOnRevitThread<T>(Func<Document, T> action)
        {
            var tcs = new TaskCompletionSource<T?>();

            _eventHandler.SetWork(uiDoc =>
            {
                try
                {
                    var result = action(uiDoc.Document);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            _externalEvent.Raise();
            return tcs.Task;
        }
    }
}
