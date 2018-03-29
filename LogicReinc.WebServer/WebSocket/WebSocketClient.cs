using LogicReinc.WebServer.Components.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.WebSocket
{
    public abstract class WebSocketClient : IDisposable
    {
        public static long MaximumPackageSize = 40960;

        public WebSocketContext Context { get; private set; }
        public System.Net.WebSockets.WebSocket Socket { get; private set; }
        public WebSocketDescriptor Descriptor { get; private set; }

        public bool Dead { get; set; }
        public bool Active { get; private set; }
        //public abstract bool HandleIndependantly { get; }


        public event Action Disconnected;
        public event Action<string> OnTextPackage;
        public event Action<byte[]> OnBinaryPackage;



        private bool _ishandling = false;
        
        public void Initialize(HttpRequest request, WebSocketDescriptor descriptor, WebSocketContext context)
        {
            request.ReadLock = true;
            Context = context;
            Socket = context.WebSocket;
            Descriptor = descriptor;
        }

        public void Start()
        {
            HandlePackages();
        }

        public async Task Disconnect(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string statusTxt = "")
        {
            Active = false;
            Dead = true;
            await Socket.CloseAsync(status, statusTxt, CancellationToken.None);
            Dispose();
        }


        public async Task Send(string data)
        {
            await Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(data)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task Send(byte[] data)
        {
            await Socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        
        protected async void HandlePackages()
        {
            if (_ishandling)
                return;
            _ishandling = true;
            Active = true;
            Dead = false;

            try
            {
                while (Socket.State == WebSocketState.Open && Active && !Dead)
                {
                    await HandleOne();
                }
            }
            catch(Exception ex)
            {
                Descriptor.Server.Log("WebClient:HandleContext", "Failed handling packages:" + ex.Message);
            }
            Dead = true;
            Active = false;
            _ishandling = false;
            if (Disconnected != null)
                Disconnected();
            Dispose();
        }

        internal async Task HandleOne()
        {
            byte[] buffer = new byte[4096];
            WebSocketReceiveResult result = await Socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            byte[] bResult = null;

            if(!result.EndOfMessage)
                using (MemoryStream str = new MemoryStream())
                {
                    str.Write(buffer, 0, result.Count);
                    do
                    {
                        result = await Socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        str.Write(buffer, 0, result.Count);

                        if(str.Length > MaximumPackageSize)
                        {
                            Disconnect(WebSocketCloseStatus.MessageTooBig);
                            return;
                        }
                    }
                    while (!result.EndOfMessage);
                    bResult = str.ToArray();
                }
            else
            {
                bResult = new byte[result.Count];
                Array.Copy(buffer, bResult, result.Count);
            }

            try
            {
                switch (result.MessageType)
                {
                    case WebSocketMessageType.Close:
                        Disconnect();
                        break;
                    case WebSocketMessageType.Binary:
                        if (OnBinaryPackage != null)
                            OnBinaryPackage(bResult);
                        HandleBinary(bResult);
                        break;
                    case WebSocketMessageType.Text:
                        string msg = Encoding.UTF8.GetString(bResult);
                        if (OnTextPackage != null)
                            OnTextPackage(msg);
                        HandleText(msg);
                        break;
                }
            }
            catch(Exception ex)
            {
                Descriptor.Server.Log("WebSocketClient:HandleOne", "Failed to handle response: " + ex.Message);
            }
        }

        public abstract void HandleText(string msg);
        public abstract void HandleBinary(byte[] data);



        public void Dispose()
        {
            Dead = true;
            Descriptor.Container.RemoveClient(this);
            Socket.Dispose();
        }
    }
}
