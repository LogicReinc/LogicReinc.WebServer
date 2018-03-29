using LogicReinc.Collections;
using LogicReinc.WebServer.Components.WebSocket;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace LogicReinc.WebServer.WebSocket
{

    public interface IWebSocketClientContainer
    {
        void RemoveClient(WebSocketClient client);
        void HandleRequest(WebSocketDescriptor descriptor, HttpRequest request);
    }

    public class WebSocketClientContainer<T> : IWebSocketClientContainer where T : WebSocketClient, new()
    {
        public TSList<WebSocketClient> Clients { get; private set; } = new TSList<WebSocketClient>();

        public event Action<T> ClientConnected;
        public event Action<T> ClientDisconnected;

        public void RemoveClient(WebSocketClient client)
        {
            Clients.Remove(client);
            if(ClientDisconnected !=null)
            {
                try
                {
                    ClientDisconnected((T)client);
                }
                catch { }
            }
        }

        public void RemoveDeadClients()
        {
            Clients.Remove(x => x.Dead);
        }

        public void Broadcast(string str)
        {
            foreach(WebSocketClient client in Clients.ToList())
            {
                if(!client.Dead && client.Active)
                    client.Send(str);
            }
        }
        public void Broadcast(byte[] data)
        {
            foreach (WebSocketClient client in Clients.ToList())
            {
                if (!client.Dead && client.Active)
                    client.Send(data);
            }
        }

        public async void HandleRequest(WebSocketDescriptor descriptor, HttpRequest request)
        {
            try
            {
                request.ReadLock = true;

                WebSocketContext wsC = await request.Context.AcceptWebSocketAsync(null);

                WebSocketClient client = new T();
                client.Initialize(request, descriptor, wsC);
                Clients.Add(client);
                client.Start();

                if (ClientConnected != null)
                {
                    try
                    {
                        ClientConnected((T)client);
                    }
                    catch { }
                }
            }
            catch(Exception ex)
            {
                descriptor.Server.Log("WebSocketClientContainer:HandleRequest", "Failed to accept Websocket context:" + ex.Message);
            }
        }
    }
}
