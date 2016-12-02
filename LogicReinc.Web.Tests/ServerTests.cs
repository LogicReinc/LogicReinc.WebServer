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

namespace LogicReinc.Web.Tests
{
    [TestClass]
    public class ServerTests
    {
        static HttpServer server;
        static WebClient client = new WebClient();

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
            server.WorkerCount = 15;
            server.AddRoute("/", (x) => x.Write(SimpleRouteData));
            server.AddRoute((x) => x.Parameters.ContainsKey("triggerConditional"), (r) => r.Write(ConditionalRouteData));
            server.AddRoute<TestController>("/controller");
            server.AddRoute<SyncController>("/sync");
            server.AddRoute<SecurityController<SecuritySettings>>("/security");
            server.Start();
        }

        public class SecuritySettings : ISecuritySettings
        {
            public bool HasUserData => true;

            public int GetTokenLevel(string token)
            {
                string data = 
                    (string)SecurityController<SecuritySettings>.GetTokenData(token);
                if (data == "admin")
                    return 5;
                else
                    return 1;
            }

            public object GetUserData(string username)
            {
                return username;
            }

            public bool VerifyUser(string username, string password)
            {
                if (username.ToLower() == "admin" && password == AdminPassword)
                    return true;
                else if (OtherUsers.Contains(username.ToLower()))
                    return true;
                return false;
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


        //Keeps server online till debug stopped. Used for outside-VS testing.
        [TestMethod]
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
            Console.WriteLine(count);
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
            Console.WriteLine(cap);
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
            Console.WriteLine(cap);
            Task.WaitAll(tasks.ToArray());
        }

        [TestMethod]
        public void ControllerConcurrentTest()
        {
            Stopwatch w = new Stopwatch(); w.Start();
            server.IOHandling = WebServer.Enums.IOHandlingType.WorkerPool; //Default = WorkerPool
            int cap = 15;
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
                        Console.WriteLine($"Client callback after {wClient.ElapsedMilliseconds}");
                        return res;
                    }));
                }
                Console.WriteLine(cap);
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
                        Console.WriteLine($"Client callback after {wClient.ElapsedMilliseconds}");
                        return res;
                    }));
                }
                Console.WriteLine(cap);
                Task.WaitAll(tasks.ToArray());
            }

            w.Stop();
            Console.WriteLine($"Total completion in: {w.ElapsedMilliseconds}");
        }


    }
}
