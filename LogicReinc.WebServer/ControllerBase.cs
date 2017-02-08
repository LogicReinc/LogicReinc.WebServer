using LogicReinc.WebServer.Enums;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer
{
    public class ControllerBase
    {
        
        public HttpServer Server { get; private set; }
        public HttpRequest Request { get; private set; }

        public bool Authenticated => Request.Authenticated;
        public int AuthenticationLevel => Request.AuthenticationLevel;

        public dynamic DynamicModel { get; protected set; } = new ExpandoObject();

        public ControllerBase() { }
        public ControllerBase(HttpRequest request)
        {
            Request = request;
        }
        

        internal void Init(HttpServer server, HttpRequest request)
        {
            Server = server;
            Request = request;
        }

        public virtual void Registration(HttpServer server)
        {

        }

        //Wrapper Methods
        public void Write(string data) => Request.Write(data);
        public void Write(byte[] data) => Request.Write(data);
        public void Write(object data, BodyType type = BodyType.JSON) => Request.Write(data, type);

        public void Stream(Stream stream) => Request.Stream(stream);

        public void Redirect(string newUrl) => Request.Redirect(newUrl);
        public void SetCode(int code) => Request.SetCode(code);
        public void ThrowCode(int code) => Request.ThrowCode(code);
        public void ThrowCode(int code, byte[] data) => Request.ThrowCode(code, data);
        public void ThrowCode(int code, string data) => Request.ThrowCode(code, data);

        //Wrapper Properties
        public string Token => Request.Token;
        public object TokenData => Request.TokenData;

    }
}
