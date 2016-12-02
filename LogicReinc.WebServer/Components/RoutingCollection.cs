using LogicReinc.SyntaxSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LogicReinc.Extensions;

namespace LogicReinc.WebServer.Components
{
    public class RoutingCollection
    {
        public HttpServer Server { get; private set; 
}
        public RoutingCollection(HttpServer server)
        {
            Server = server;
        }

        private Dictionary<string, string> Files { get; } = new Dictionary<string, string>();
        private Dictionary<string, Action<HttpRequest>> Routings { get; } = new Dictionary<string, Action<HttpRequest>>();
        private Dictionary<string, ControllerDescriptor> Controllers { get; } = new Dictionary<string, ControllerDescriptor>();
        private Dictionary<Func<HttpRequest, bool>, Action<HttpRequest>> ConditionalRouting { get; } = new Dictionary<Func<HttpRequest, bool>, Action<HttpRequest>>();

        public List<ControllerRoute> GetControllers() => Controllers.Select(x => new ControllerRoute(x.Key, x.Value)).ToList();

        public void AddFileRoute(string url, string path)
        {
            Files.Add(url.ToLower(), path);
        }

        public void AddRoute(string url, Action<HttpRequest> request)
        {
            Routings.Add(url.ToLower(), request);
        }
        public void AddRoute<T>(string url, bool apiWrapped = true)
        {
            ControllerDescriptor descriptor = ControllerDescriptor.GetTemplate<T>(Server, apiWrapped);
            descriptor.Register();
            Controllers.Add(url.ToLower(), descriptor);
        }
        public void AddRoute(string url, ControllerDescriptor descriptor)
        {
            Controllers.Add(url.ToLower(), descriptor);
        }
        public void AddRoute(Func<HttpRequest, bool> condition, Action<HttpRequest> action)
        {
            ConditionalRouting.Add(condition, action);
        }
        
        public void Clear()
        {
            Routings.Clear();
            Controllers.Clear();
            ConditionalRouting.Clear();
        }


        public bool ExecuteFile(HttpRequest request)
        {
            string lPath = request.Url.Path.ToLower();
            bool found = false;
            try
            {
                if (Files.ContainsKey(lPath))
                {
                    found = true;
                    string filePath = Files[lPath];
                    string mime = new FileInfo(filePath).GetMimeType();
                    request.Response.ContentType = mime;
                    request.Write(Server.FileCache.LoadFile(filePath));
                    
                }
            }
            finally
            {
                if(found)
                request.Close();
            }
            return found;
        }
        public bool ExecuteConditional(HttpRequest request)
        {
            foreach (var conditional in ConditionalRouting)
                if (conditional.Key(request))
                {
                    conditional.Value(request);
                    return true;
                }
            return false;
        }
        public bool ExecuteRouting(HttpRequest request)
        {
            string lPath = request.Url.Path.ToLower();
            var route = Routings.FirstOrDefault(x => x.Key == lPath).Value;

            if (route != null)
            {
                route(request);
                if (!request.IsClosed)
                    request.Close();
                return true;
            }
            return false;
        }
        public bool ExecuteController(HttpRequest request)
        {

            string controllerPath = request.Url.Path.ToLower();
            string trimPath = controllerPath.Trim('/');
            string method;
            if (!trimPath.Contains("/"))
            {
                method = "";
            }
            else
            {
                int lastDevider = controllerPath.LastIndexOf('/');

                method = controllerPath.Substring(lastDevider + 1);
                controllerPath = controllerPath.Substring(0, lastDevider);
            }

            foreach (var controller in Controllers)
                if(controller.Key == controllerPath)
                    return controller.Value.ExecuteController(method, request);

            return false;
        }


        public class Route
        {
            public string Path { get; set; }

            public Route(string path)
            {
                Path = path;
            }
        }

        public class ControllerRoute : Route
        {
            public ControllerDescriptor Controller { get; set; }
            
            public ControllerRoute(string path, ControllerDescriptor controller ): base(path)
            {
                Controller = controller;
            }
        }
    }
}
