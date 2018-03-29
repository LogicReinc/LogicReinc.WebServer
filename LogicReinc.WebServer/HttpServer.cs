using LogicReinc.SyntaxSugar;
using LogicReinc.Threading;
using LogicReinc.WebServer.Components;
using LogicReinc.WebServer.Components.WebSocket;
using LogicReinc.WebServer.Enums;
using LogicReinc.WebServer.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static LogicReinc.WebServer.Components.ControllerDescriptor;

namespace LogicReinc.WebServer
{
    public delegate void OnControllerRequestDelegate(HttpRequest request, CallDescriptor call);
    public delegate void OnRequestDelegate(HttpRequest request);
    public delegate void OnLogDelegate(string location, string log);
    public delegate void OnExceptionDelegate(string location, Exception ex);
    public class HttpServer
    {
        private HttpListener _listener;

        //Routing
        public RoutingCollection Routing { get; set; }

        public int WorkerCount { get; set; } = Environment.ProcessorCount * 3;
        private WorkerPool<HttpRequest> ThreadPool { get; set; } 

        //Public
        public bool Active { get; private set; }

        public string ReverseProxy { get; set; }
        public bool AllowIPForward { get; set; }

        public int SslPort { get; private set; } = 0;
        public int Port { get; private set; } = 80;

        public bool AllowWebSocketProtocol { get; set; } = false;

        public bool Debug { get; set; } = false;

        public List<string> Bindings { get; } = new List<string>();


        private RazorCache _razorCacheInstance = null;
        public RazorCache RazorCache {
            get
            {
                if (_razorCacheInstance == null)
                    _razorCacheInstance = new RazorCache();
                return _razorCacheInstance;
            }
            private set
            {
                _razorCacheInstance = value;
            }
        }
        public FileCache FileCache { get; private set; } = new FileCache();

        public BodyType DefaultRequestType { get; set; } = BodyType.JSON;
        public BodyType DefaultResponseType { get; set; } = BodyType.JSON;
        public List<BodyType> AllowedResponseTypes { get; set; } = new List<BodyType>();

        public IOHandlingType IOHandling { get; set; } = IOHandlingType.WorkerPool;

        public event OnRequestDelegate OnDefaultRequest;
        public event OnRequestDelegate OnRequest;
        public event OnRequestDelegate OnRequestPost;
        public event OnControllerRequestDelegate OnPreController;
        public event OnLogDelegate OnLog;
        public event OnExceptionDelegate OnException;

        public string DefaultResponseTypeMime
        {
            get
            {
                switch(DefaultResponseType)
                {
                    case BodyType.JSON:
                        return "application/json";
                    case BodyType.XML:
                        return "application/xml";
                    case BodyType.UrlEncoded:
                        return "x-www-form-urlencoded";
                    default: return "";
                }
            }
        }


        public HttpServer()
        {
            Routing = new RoutingCollection(this);
        }

        public HttpServer(int port)
        {
            Routing = new RoutingCollection(this);
            Port = port;
        }

        public HttpServer(int sslPort, int port)
        {
            Routing = new RoutingCollection(this);
            Port = port;
            SslPort = sslPort;
        }

        //Routing
        public void AddFile(string url, string path)
        {
            FileInfo i = new FileInfo(path);
            
            if (!i.Exists)
                throw new FileNotFoundException($"File '{path}' not found");

            if (i.Extension.ToLower() == ".cshtml")
            {
                try
                {
                    RazorCache.AddTemplate(i.Name, File.ReadAllText(i.FullName));
                }
                catch(Exception ex)
                {
                    if (OnException != null) OnException("AddFile", ex);
                    if (Debug)
                        RazorCache.AddTemplate(i.Name, $"Exception found in Razor Template:\n {ex.Message.Replace("@", "@@")}");
                    else
                        RazorCache.AddTemplate(i.Name, $"Exception found in Razor Template, enable debug to see exception");
                }
            }
            Routing.AddFileRoute(url, i.FullName);
        }
        public void AddDirectory(string url, string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);

            foreach (DirectoryInfo subDir in dir.GetDirectories())
                AddDirectory(Path.Combine(url, subDir.Name).Replace('\\', '/'), Path.Combine(dir.FullName, subDir.Name));

            foreach (FileInfo file in dir.GetFiles())
                AddFile(Path.Combine(url, file.Name).Replace('\\','/'), Path.Combine(path, file.Name));
        }
        //Route: Action
        public void AddRoute(string url, Action<HttpRequest> action)
        {
            Routing.AddRoute(url, action);
        }
        //Route: Controller
        public void AddRoute<T>(string url)
        {
            Routing.AddRoute<T>(url);
        }
        public void AddRoute(string url, ControllerDescriptor descriptor)
        {
            Routing.AddRoute(url, descriptor);
        }
        //Route: WebSocket
        public WebSocketClientContainer<T> AddWebSocket<T>(string url, bool requiresToken = false, int level = 0) where T: WebSocketClient, new()
        {
            AllowWebSocketProtocol = true;
            return Routing.AddWebSocket<T>(url,requiresToken, level);
        }
        public void AddWebSocket(string url, WebSocketDescriptor descriptor)
        {
            AllowWebSocketProtocol = true;
            Routing.AddWebSocket(url, descriptor);
        }

        //Route: Conditional
        public void AddRoute(Func<HttpRequest, bool> condition, Action<HttpRequest> action)
        {
            Routing.AddRoute(condition, action);
        }

        //Route: Passthrough
        public void AddPassthrough(Func<HttpRequest,bool> condition, HttpServer server)
        {
            Routing.AddPassthrough(condition, server);
        }
        
        public void AddServer(string[] bindings, HttpServer server)
        {
            bindings = bindings.Select(x => x.ToLower()).ToArray();
            AddRoute((r) => bindings.Contains(r.Url.Domain.ToLower()), (r) => server.HandleRequest(r));
        }

        public void Start()
        {
            if (Active == false)
            {
                ThreadPool = new WorkerPool<HttpRequest>(WorkerCount, 10);
                _listener = new HttpListener();

                if (SslPort != 0)
                {
                    _listener.Prefixes.Add($"https://*:{SslPort}/");
                    if (AllowWebSocketProtocol)
                        ;// _listener.Prefixes.Add($"wss://*:{Port}/");
                }
                if (Port != 0)
                {
                    _listener.Prefixes.Add($"http://*:{Port}/");
                    if (AllowWebSocketProtocol)
                        ;// _listener.Prefixes.Add($"ws://*:{Port}/");
                }

                foreach (string binding in Bindings)
                    _listener.Prefixes.Add(binding);

                Active = true;
                _listener.Start();
                Log("Start", "Server Started");

                Task.Run(() =>
                {
                    while (Active)
                    {
                        var result = _listener.BeginGetContext(HandleContext, _listener);
                        result.AsyncWaitHandle.WaitOne();
                    }
                });
            }
        }

        public void Stop()
        {
            Active = false;

            if (ThreadPool != null)
                ThreadPool.SetWorkerCount(0);

            if (_listener == null)
                _listener.Close();
        }

        private void HandleContext(IAsyncResult ac)
        {
            HttpListener listener = (HttpListener)ac.AsyncState;
            HttpListenerContext context = listener.EndGetContext(ac);

            HttpRequest request = new HttpRequest(context);
            request.Server = this;

            switch(IOHandling)
            {
                case IOHandlingType.Synchronously:
                    HandleRequest(request);
                    break;
                case IOHandlingType.DefaultThreadpool:
                    System.Threading.ThreadPool.QueueUserWorkItem((s) => HandleRequest(request));
                    break;
                case IOHandlingType.TaskRun:
                    Task.Run(() => HandleRequest(request));
                    break;
                case IOHandlingType.WorkerPool:
                    ThreadPool.QueueWork(request, HandleRequest);
                    break;
            }
        }

        public void HandleRequest(HttpRequest req)
        {
            try
            {
                if (req.Server != this)
                    req.Server = this;

                Log("HandleRequest", $"{req.RemoteAddress}->{req.Url.Path}");

                HttpRequest request = req;
                {
                    if (Routing.ExecuteConditionalPassthroughs(request))
                        return;

                    //On each request
                    if (OnRequest != null)
                        OnRequest(request);
                    if (request.IsClosed)
                        return;

                    if (OnRequestPost != null)
                        OnRequestPost(request);

                    if (request.IsClosed)
                        return;

                    //Routing
                    if (Routing.ExecuteConditional(request))
                        return;

                    if (Routing.ExecuteRouting(request))
                        return;

                    if (Routing.ExecuteController(request))
                        return;

                    if (Routing.ExecuteFile(request))
                        return;

                    if (AllowWebSocketProtocol && Routing.ExecuteWebSocket(request))
                        return;

                    //Default Callback
                    if (OnDefaultRequest != null && !request.IsClosed)
                        OnDefaultRequest(request);
                }
            
            }
            catch(Exception ex)
            {

                if (OnException != null)
                    OnException("HandleRequest", ex);
                if (OnLog != null)
                    OnLog("HandleRequest", $"Exception when handling request: {ex.Message}");
            }
            finally
            {
                if (!req.IsClosed && !req.DisableAutoHandling)
                    try
                    {
                        req.Close();
                    }
                    catch { }
            }
        }

        public bool IsAllowedResponse(BodyType type)
        {
            if (AllowedResponseTypes.Count == 0)
                return true;
            if (AllowedResponseTypes.Contains(type))
                return true;
            return false;
        }

        public void Log(string location, string msg)
        {
            if (OnLog != null)
                OnLog(location, msg);
        }

        //Event Calls
        internal void PreControllerCheck(HttpRequest request, CallDescriptor desc)
        {
            if (OnPreController != null)
                OnPreController(request, desc);
        }
    }
}
