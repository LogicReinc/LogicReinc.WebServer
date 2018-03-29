using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogicReinc.WebServer;
using System.Net;
using Newtonsoft.Json;
using LogicReinc.API;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using LogicReinc.WebServer.Attributes;
using LogicReinc.WebServer.Enums;
using LogicReinc.Parsing;
using static LogicReinc.Web.Tests.ServerTests.TestController;
using LogicReinc.WebSercer.Controllers;
using LogicReinc.WebServer.Controllers;
using LogicReinc.WebServer.Exceptions;
using LogicReinc.Security.TokenSystem;
using System.Net.WebSockets;
using System.Text;
using LogicReinc.WebServer.WebSocket;
using System.Linq;

namespace LogicReinc.Web.Tests
{
    [TestClass]
    public class ServerTests
    {
        static HttpServer server;
        static WebClient client = new WebClient();
        static WebSocketClientContainer<WebSocketTestClient> testClients = null;

        public const int Port = 9990;
        public static string Address = $"http://localhost:{Port}";

        //Test constants
        public const string SimpleRouteData = "Simple Route";
        public const string ConditionalRouteData = "Conditional Route";
        public const string SimpleControllerData = "Simple Controller Test";

        public const string AdminUsername = "admin";
        public const string AdminPassword = "AbcAbc123";

        public static List<string> OtherUsers { get; } = new List<string>() { "user1", "user2", "user3" }; 


        [ClassInitialize]
        public static void Init(TestContext context)
        {
            server = new HttpServer(Port);
            server.OnLog += (l, m) => System.Console.WriteLine(l + "->" + m); 
            server.WorkerCount = 15;
            server.AddRoute("/", (x) => x.Write(SimpleRouteData));
            server.AddRoute((x) => x.Parameters.ContainsKey("triggerConditional"), (r) => r.Write(ConditionalRouteData));
            server.AddRoute<TestController>("/controller");
            server.AddRoute<SyncController>("/sync");
            server.AddRoute<SecurityController<SecuritySettings>>("/security");
            testClients = server.AddWebSocket<WebSocketTestClient>("/websocket");
            server.AddWebSocket<WebSocketAdminClient>("/adminsocket", true, 5);
            server.Start();
        }

        public class SecuritySettings : ISecuritySettings
        {
            public bool HasUserData => true;
            public bool SendUserData => false;
            public bool IsIPBoundToken => true;
            public int TokenDuration => 3600;

            public int GetTokenLevel(Token token)
            {
                string data = 
                    (string)token.Data;
                if (data == "admin")
                    return 5;
                else
                    return 1;
            }

            public object GetUserData(string username)
            {
                return username;
            }

            public bool VerifyUser(string username, string password, out object userData)
            {
                userData = username;
                if (username.ToLower() == "admin" && password == AdminPassword)
                    return true;
                else if (OtherUsers.Contains(username.ToLower()))
                    return true;
                return false;
            }

            public void PrepareRequest(Token token, HttpRequest request)
            {

            }
        }

        //[domain]/Controller/*
        public class TestController : ControllerBase
        {
            public void Test()
            {
                Request.Write(SimpleControllerData);
            }

            public void TestSleep(int wait)
            {
                Thread.Sleep(wait);
            }

            [MethodDescriptorAttribute(PostParameter = "jsonObj", RequestType = BodyType.JSON, ResponseType = BodyType.JSON)]
            public TestSub TestPostJSON(TestSub jsonObj)
            {
                return jsonObj;
            }

            [MethodDescriptorAttribute(PostParameter = "xmlObj", RequestType = BodyType.XML, ResponseType = BodyType.XML)]
            public TestSub TestPostXML(TestSub xmlObj)
            {

                return xmlObj;
            }

            public string TestAPIResponse()
            {
                return SimpleControllerData;
            }

            public TestSub TestAPIResponseObj()
            {
                return new TestSub()
                {
                    IntProperty = 123,
                    StringProperty = SimpleControllerData
                };
            }


            [RequiresToken]
            public void AuthTest()
            {
                Request.Write("true");
                Request.Close();
            }
            [RequiresToken(5)]
            public void AuthLvl5Test()
            {
                Request.Write("true");
                Request.Close();
            }


            public class TestSub
            {
                public string StringProperty { get; set; }
                public int IntProperty { get; set; }
            }
        }

        public class WebSocketTestClient : WebSocketClient
        {
            public override void HandleBinary(byte[] data)
            {
                throw new NotImplementedException();
            }

            public override void HandleText(string msg)
            {
                if (msg == "Ping")
                    Send("Pong");
                if (msg == "DisconnectMe")
                    Disconnect();
            }
        }
        public class WebSocketAdminClient : WebSocketClient
        {
            public override void HandleBinary(byte[] data)
            {
                throw new NotImplementedException();
            }

            public override void HandleText(string msg)
            {
                if (msg == "Ping")
                    Send("Pong");
                if (msg.StartsWith("Broadcast:"))
                    testClients.Broadcast(msg.Substring("Broadcast:".Length));

            }
        }


        //Keeps server online till debug stopped. Used for outside-VS testing.
        //[TestMethod]
        public void HostTest()
        {
            while (true)
                Thread.Sleep(1000);
        }




        //Basic
        [TestMethod]
        public void SimpleRoute()
        {
            string data = client.DownloadString(Address);

            Assert.AreEqual(SimpleRouteData, data, $"Received data incorrect, found: {data}");
        }

        [TestMethod]
        public void ConditionalRoute()
        {
            string data = client.DownloadString($"{Address}?triggerConditional=");

            Assert.AreEqual(ConditionalRouteData, data, $"Received data incorrect, found {data}");
        }

        [TestMethod]
        public void SimpleController()
        {
            string data = client.DownloadString($"{Address}/controller/Test");
            Assert.AreEqual(SimpleControllerData, data, $"Received data incorrect, found {data}, Found: {data}");
        }

        [TestMethod]
        public void SimpleControllerAPI()
        {
            string raw = client.DownloadString($"{Address}/controller/TestAPIResponse");
            var data = JsonConvert.DeserializeObject<APIWrap<string>>(raw);
            Assert.AreEqual(SimpleControllerData, data.Result, $"Received data incorrect, Exception: {data.Exception?.Message}");
        }




        [TestMethod]
        public void Authentication()
        {

            string data = client.DownloadString($"{Address}/controller/AuthTest");
            Assert.AreNotEqual("true", data, $"Server should not allow access to this controller");

            APIWrap result = JsonConvert.DeserializeObject<APIWrap>(data);
            Assert.IsFalse(result.Success, $"Call should not be succesfull");
            Assert.AreEqual(typeof(ForbiddenException).Name, result.Exception.Type, $"Server giving wrong exception");

            data = client.UploadString($"{Address}/security/GetToken", JsonConvert.SerializeObject(new SecurityUser()
            {
                Username = "user1",
                Password = "sadgf"
            }));
            APIWrap<Token> tokenResult = JsonConvert.DeserializeObject<APIWrap<Token>>(data);
            Assert.IsTrue(tokenResult.Success, $"Failed GetToken due to [{tokenResult.Exception?.Type}]:{tokenResult.Exception?.Message}");

            data = client.DownloadString($"{Address}/controller/AuthTest?t={tokenResult.Result.AccessToken}");
            Assert.AreEqual("true", data, $"No access given to controller despite authentication");

            data = client.DownloadString($"{Address}/controller/AuthLvl5Test?t={tokenResult.Result.AccessToken}");
            Assert.AreNotEqual("true", data, $"Server should not allow access to this controller");

            result = JsonConvert.DeserializeObject<APIWrap>(data);
            Assert.IsFalse(result.Success, $"Call should not be succesfull");
            Assert.AreEqual(typeof(ForbiddenException).Name, result.Exception.Type, $"Server giving wrong exception");

            data = client.UploadString($"{Address}/security/GetToken", JsonConvert.SerializeObject(new SecurityUser()
            {
                Username = AdminUsername,
                Password = AdminPassword
            }));
            tokenResult = JsonConvert.DeserializeObject<APIWrap<Token>>(data);
            Assert.IsTrue(tokenResult.Success, $"Failed GetToken for admin due to [{tokenResult.Exception?.Type}]:{tokenResult.Exception?.Message}");

            data = client.DownloadString($"{Address}/controller/AuthLvl5Test?t={tokenResult.Result.AccessToken}");
            Assert.AreEqual("true", data, $"No access given to admin controller despite authentication");
        }

        //Attribute Calls
        [TestMethod]
        public void ControllerJsonAPI()
        {
            TestController.TestSub sub = new TestController.TestSub()
            {
                IntProperty = 123,
                StringProperty = SimpleControllerData
            };

            string raw = client.UploadString($"{Address}/controller/TestPostJSON", JsonConvert.SerializeObject(sub));
            var data = JsonConvert.DeserializeObject<APIWrap<TestController.TestSub>>(raw);
            Assert.AreEqual(SimpleControllerData, data.Result.StringProperty, $"Received data incorrect, Exception: {data.Exception?.Message}");
        }
        [TestMethod]
        public void ControllerXmlAPI()
        {
            TestController.TestSub sub = new TestController.TestSub()
            {
                IntProperty = 123,
                StringProperty = SimpleControllerData
            };

            string raw = client.UploadString($"{Address}/controller/TestPostXML", XmlConvert.SerializeObject(sub));
            var data = XmlConvert.DeserializeObject<APIWrap>(raw).As<TestSub>();
            Assert.AreEqual(SimpleControllerData, data.Result.StringProperty, $"Received data incorrect, Exception: {data.Exception?.Message}");
        }



        [TestMethod]
        public void SimpleControllerAPIObj()
        {
            Stopwatch w = new Stopwatch();w.Start();
            int count = 0;
            while(w.ElapsedMilliseconds < 1000)
            {
                string raw = client.DownloadString($"{Address}/controller/TestAPIResponseObj");
                var data = JsonConvert.DeserializeObject<APIWrap<TestController.TestSub>>(raw);
                Assert.IsTrue(data.Success, $"Request failed due to {data.Exception?.Message}");
                Assert.AreEqual(123, data.Result.IntProperty, $"Received data incorrect, found {data.Result.IntProperty}");
                Assert.AreEqual(SimpleControllerData, data.Result.StringProperty, $"Received data incorrect, found {data.Result.StringProperty}");
                count++;
            }
            System.Console.WriteLine(count);
        }


        [TestMethod]
        public void ControllerSpeedTest()
        {
            List<Task<string>> tasks = new List<Task<string>>();
            int cap = 3000;
            for (int i = 0; i < cap; i++)
            {
                WebClient client = new WebClient();
                tasks.Add(Task.Run(() =>
                client.DownloadString($"{Address}/controller/Test")));
            }
            System.Console.WriteLine(cap);
            Task.WaitAll(tasks.ToArray());
        }

        [TestMethod]
        public void ControllerAPIObjSpeedTest()
        {
            List<Task<string>> tasks = new List<Task<string>>();
            int cap = 3000;
            for (int i = 0; i < cap; i++)
            {
                WebClient client = new WebClient();
                tasks.Add(Task.Run(() => 
                client.DownloadString($"{Address}/controller/TestAPIResponseObj")));
            }
            System.Console.WriteLine(cap);
            Task.WaitAll(tasks.ToArray());
        }

        [TestMethod]
        public void ControllerConcurrentTest()
        {
            Stopwatch w = new Stopwatch(); w.Start();
            server.IOHandling = WebServer.Enums.IOHandlingType.WorkerPool; //Default = WorkerPool
            int cap = 25;
            for (int volley = 0; volley < 10; volley++)
            {
                List<Task<string>> tasks = new List<Task<string>>();
                for (int i = 0; i < cap; i++)
                {
                    WebClient client = new WebClient();
                    Stopwatch wClient = new Stopwatch(); wClient.Start();
                    tasks.Add(Task.Run(() =>
                    {
                        string res = client.DownloadString($"{Address}/controller/TestSleep?wait=1000");
                        wClient.Stop();
                        System.Console.WriteLine($"Client callback after {wClient.ElapsedMilliseconds}");
                        return res;
                    }));
                }
                System.Console.WriteLine(cap);
                Task.WaitAll(tasks.ToArray());
            }
            Thread.Sleep(3000);
            for (int volley = 0; volley < 10; volley++)
            {
                List<Task<string>> tasks = new List<Task<string>>();
                for (int i = 0; i < cap; i++)
                {
                    WebClient client = new WebClient();
                    Stopwatch wClient = new Stopwatch(); wClient.Start();
                    tasks.Add(Task.Run(() =>
                    {
                        string res = client.DownloadString($"{Address}/controller/TestSleep?wait=1000");
                        wClient.Stop();
                        System.Console.WriteLine($"Client callback after {wClient.ElapsedMilliseconds}");
                        return res;
                    }));
                }
                System.Console.WriteLine(cap);
                Task.WaitAll(tasks.ToArray());
            }

            w.Stop();
            System.Console.WriteLine($"Total completion in: {w.ElapsedMilliseconds}");
        }


        //Websocket
        [TestMethod]
        public void WebsocketConnection()
        {
            Task.Run(async () =>
            {
                ClientWebSocket socket = new ClientWebSocket();
                await socket.ConnectAsync(new Uri("ws://localhost:9990/websocket"), CancellationToken.None);
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Ping")), WebSocketMessageType.Text, true, CancellationToken.None);
                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                string resultString = Encoding.UTF8.GetString(buffer, 0, result.Count);

                Assert.AreEqual(resultString, "Pong");
            }).Wait();
        }
        [TestMethod]
        public void WebsocketConnectionHandling()
        {
            Task.Run(async () =>
            {
                ClientWebSocket socket = new ClientWebSocket();
                await socket.ConnectAsync(new Uri("ws://localhost:9990/websocket"), CancellationToken.None);

                Assert.AreEqual(1, testClients.Clients.Length);
                testClients.Clients[0].Disconnected += () =>
                {
                    
                };


                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Ping")), WebSocketMessageType.Text, true, CancellationToken.None);
                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                string resultString = Encoding.UTF8.GetString(buffer, 0, result.Count);

                Assert.AreEqual(resultString, "Pong");

                socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                Thread.Sleep(2000);
                Assert.AreEqual(0, testClients.Clients.Length);

                socket = new ClientWebSocket();
                await socket.ConnectAsync(new Uri("ws://localhost:9990/websocket"), CancellationToken.None);

                Assert.AreEqual(1, testClients.Clients.Length);

                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("DisconnectMe")), WebSocketMessageType.Text, true, CancellationToken.None);

                Thread.Sleep(100);
                Assert.AreEqual(0, testClients.Clients.Length);
            }).Wait();
        }
        [TestMethod]
        public void WebsocketMulticlient()
        {
            Task.Run(async () =>
            {
                List<Task> tasks = new List<Task>();
                int ccount = 1000;
                for(int i = 0; i < ccount; i++)
                {
                    int a = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        Stopwatch watch = new Stopwatch();
                        watch.Start();

                        byte[] buffer = new byte[1024];
                        ClientWebSocket socket = new ClientWebSocket();
                        await socket.ConnectAsync(new Uri("ws://localhost:9990/websocket"), CancellationToken.None);


                       
                        for (int x = 0; x < 5; x++)
                        {
                            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Ping")), WebSocketMessageType.Text, true, CancellationToken.None);

                            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            string resultString = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            Assert.AreEqual(resultString, "Pong");
                            System.Console.WriteLine(a.ToString() + " - " + x.ToString());
                        }

                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);


                        watch.Stop();
                        System.Console.WriteLine("Task(" + a.ToString() + "): " + watch.ElapsedMilliseconds.ToString());
                    }));
                }

                try
                {
                    Task.WaitAll(tasks.ToArray());


                }
                catch(Exception ex)
                {
                    int s = tasks.Where(x => x.IsFaulted).Count();
                    System.Console.WriteLine("Failed:" + s.ToString());
                    throw new Exception("Failed for " + s.ToString() + " tasks out of " + ccount.ToString());
                }
                Assert.AreEqual(0, testClients.Clients.Length);
            }).Wait();
        }
        [TestMethod]
        public void WebSocketAuthenticated()
        {
            Task.Run(async () =>
            {

                string data = client.UploadString($"{Address}/security/GetToken", JsonConvert.SerializeObject(new SecurityUser()
                {
                    Username = AdminUsername,
                    Password = AdminPassword
                }));
                APIWrap<Token>  tokenResult = JsonConvert.DeserializeObject<APIWrap<Token>>(data);

                bool didSuccess = false;
                bool didCrash = false;
                try
                {
                    ClientWebSocket socket = new ClientWebSocket();
                    await socket.ConnectAsync(new Uri("ws://localhost:9990/adminsocket"), CancellationToken.None);
                    await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Ping")), WebSocketMessageType.Text, true, CancellationToken.None);
                    byte[] buffer = new byte[1024];
                    WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    string resp = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    didSuccess = true;
                }
                catch(Exception ex)
                {
                    didCrash = true;
                }

                Assert.IsTrue(!didSuccess && didCrash);


                ClientWebSocket socket2 = new ClientWebSocket();
                await socket2.ConnectAsync(new Uri("ws://localhost:9990/adminsocket?t=" + tokenResult.Result.AccessToken), CancellationToken.None);
                await socket2.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Ping")), WebSocketMessageType.Text, true, CancellationToken.None);
                byte[] buffer2 = new byte[1024];
                WebSocketReceiveResult result2 = await socket2.ReceiveAsync(new ArraySegment<byte>(buffer2), CancellationToken.None);
                string resultString = Encoding.UTF8.GetString(buffer2, 0, result2.Count);

                Assert.AreEqual(resultString, "Pong");
            }).Wait();
        }

        [TestMethod]
        public void WebsocketBroadcast()
        {
            Task.Run(async () =>
            {

                string data = client.UploadString($"{Address}/security/GetToken", JsonConvert.SerializeObject(new SecurityUser()
                {
                    Username = AdminUsername,
                    Password = AdminPassword
                }));
                APIWrap<Token> tokenResult = JsonConvert.DeserializeObject<APIWrap<Token>>(data);


                ClientWebSocket socket = new ClientWebSocket();
                await socket.ConnectAsync(new Uri("ws://localhost:9990/websocket"), CancellationToken.None);
               
                ClientWebSocket socket2 = new ClientWebSocket();
                await socket2.ConnectAsync(new Uri("ws://localhost:9990/adminsocket?t=" + tokenResult.Result.AccessToken), CancellationToken.None);
                await socket2.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Broadcast:AdminPing")), WebSocketMessageType.Text, true, CancellationToken.None);


                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                string resp = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Assert.AreEqual(resp, "AdminPing");
            }).Wait();
        }

    }
}
