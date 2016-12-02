using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer
{
    public abstract class WebServer
    {
        public bool Active { get; protected set; }
        public HttpServer Server { get; protected set; }


        public WebServer(int port, int sslPort, List<string> bindings = null)
        {
            Server = new HttpServer(sslPort, port);
            if (bindings != null)
                Server.Bindings.AddRange(bindings);
            Server.OnDefaultRequest += DefaultHandling;
        }

        public WebServer(int port, List<string> bindings = null)
        {
            Server = new HttpServer(port);
            if (bindings != null)
                Server.Bindings.AddRange(bindings);
            Server.OnDefaultRequest += DefaultHandling;
        }

        protected void Init()
        {
            Server.Routing.Clear();
            
            RegisterFiles();
            RegisterRoutes();
            RegisterControllers();
        }

        public abstract void RegisterFiles();
        public abstract void RegisterRoutes();
        public abstract void RegisterControllers();


        public abstract void DefaultHandling(HttpRequest request);


        public void Start()
        {
            if (!Active)
            {
                Init();
                Server.Start();
            }
        }

        public void Stop()
        {
            if (Active)
            {
                Server.Stop();
                Active = false;
            }
        }
    }
}
