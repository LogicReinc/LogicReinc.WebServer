using LogicReinc.WebServer.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogicReinc.WebServer.Components.WebSocket
{
    public class WebSocketDescriptor
    {
        public HttpServer Server { get; private set; }
        public IWebSocketClientContainer Container { get; private set; }

        public bool RequiresToken { get; internal set; }
        public int TokenLevel { get; internal set; }

        public WebSocketDescriptor(HttpServer server, IWebSocketClientContainer container)
        {
            Server = server;
            Container = container;
        }

        public void HandleRequest(HttpRequest request)
        {
            if(RequiresToken)
            {
                if (!request.Authenticated || request.AuthenticationLevel < TokenLevel)
                {
                    request.Close();
                    return;
                }
            }

            Container.HandleRequest(this, request);
            request.DisableAutoHandling = true;
        }
    }
}
