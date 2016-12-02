using ExampleProject.Models;
using LogicReinc.WebServer;
using LogicReinc.WebServer.Attributes;
using LogicReinc.WebServer.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleProject.Controllers
{
    public class ExampleController : ControllerBase
    {
        /// <summary>
        /// [ControllerPath]/Razor
        /// </summary>
        [MethodDescriptor(ResponseType = BodyType.Razor, RazorView = "RazorTest.cshtml")]
        public dynamic Razor()
        {
            DynamicModel.Title = "RazorTest";
            DynamicModel.Header = "TestHeader";
            DynamicModel.Items = new List<string>()
            {
                "Test1",
                "Test2",
                "Test3"
            };
            return DynamicModel;
        }


        /// <summary>
        /// [ControllerPath]/Test
        /// </summary>
        /// <returns>"Lorem ipsum"</returns>
        public void Test()
        {
            Request.Write("Lorem ipsum");
        }

        /// <summary>
        /// [ControllerPath]/TestSleep?wait=1234
        /// </summary>
        /// <param name="wait"></param>
        public void TestSleep(int wait)
        {
            Thread.Sleep(wait);
        }

        /// <summary>
        /// [ControllerPath]/TestPostJSON
        /// </summary>
        /// <param name="jsonObj">Json of ExampleObject As Body</param>
        /// <returns>Json of ExampleObject</returns>
        [MethodDescriptorAttribute(PostParameter = "jsonObj", RequestType = BodyType.JSON, ResponseType = BodyType.JSON)]
        public ExampleObject TestPostJSON(ExampleObject jsonObj)
        {
            return jsonObj;
        }

        /// <summary>
        /// [ControllerPath]/TestPostXML
        /// </summary>
        /// <param name="xmlObj">XML of ExampleObject As Body</param>
        /// <returns>XML of ExampleObject</returns>
        [MethodDescriptorAttribute(PostParameter = "xmlObj", RequestType = BodyType.XML, ResponseType = BodyType.XML)]
        public ExampleObject TestPostXML(ExampleObject xmlObj)
        {
            return xmlObj;
        }

        /// <summary>
        /// [ControllerPath]/ThrowException
        /// </summary>
        [RequiresToken(5)]
        public ExampleObject ThrowException()
        {
            throw new Exception("TestException");
        }

        /// <summary>
        /// [ControllerPath]/TestAPIResponse
        /// </summary>
        /// <returns>"Lorem ipsum TestAPIResponse"</returns>
        public string TestAPIResponse()
        {
            return "Lorem ipsum TestAPIResponse";
        }

        /// <summary>
        /// [ControllerPath]/TestAPIResponseObj
        /// </summary>
        /// <returns>Server.DefaultResponseType or AcceptHeader type of ExampleObject</returns>
        public ExampleObject TestAPIResponseObj()
        {
            return new ExampleObject()
            {
                IntProperty = 123,
                StringProperty = "Lorem Ipsum Example Model"
            };
        }
    }
}
