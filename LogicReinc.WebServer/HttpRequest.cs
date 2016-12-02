using LogicReinc.Extensions;
using LogicReinc.Parsing;
using LogicReinc.WebServer.Components;
using LogicReinc.WebServer.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer
{
    public class HttpRequest : IDisposable
    {
        public bool IsClosed { get; private set; }
        public HttpServer Server { get; set; }

        public HttpListenerContext Context { get; set; }

        public HttpListenerRequest Request => Context.Request;
        public HttpListenerResponse Response => Context.Response;

        public long ContentLength => Request.ContentLength64;

        public string LocalAddress => Request.LocalEndPoint.Address.ToString();
        public string RemoteAddress => Request.RemoteEndPoint.Address.ToString();

        public string Token { get; set; }
        public object TokenData { get; set; }
        public bool Authenticated { get; set; }
        public int AuthenticationLevel { get; set; }


        public  HttpUrl Url { get; private set; }

        private Dictionary<string, string> _parameters;
        public Dictionary<string, string> Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    Dictionary<string, string> dictionary = new Dictionary<string, string>();
                    foreach (string str in this.Context.Request.QueryString.AllKeys)
                        dictionary.Add(str, this.Context.Request.QueryString.Get(str));
                    _parameters = dictionary;
                }
                return _parameters;
            }
        }

        #region BodyData
        public Stream DataStream
        {
            get
            {
                return Request.InputStream;
            }
        }

        private byte[] _data = null;
        public byte[] Data
        {
            get
            {
                if (_data == null)
                {
                    byte[] buffer = new byte[4096];
                    int read = 0;
                    using (MemoryStream str = new MemoryStream())
                    {
                        while ((read = Request.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                            str.Write(buffer, 0, read);
                        _data = str.ToArray();
                    }
                }
                return _data;
            }
        }

        private byte[] _dataBase64 = null;
        public byte[] DataBase64
        {
            get
            {
                if (_dataBase64 == null)
                    _dataBase64 = Convert.FromBase64String(DataString);
                return _dataBase64;
            }
        }

        private string _dataString = null;
        public string DataString
        {
            get
            {
                if(_dataString == null)
                {
                    using (StreamReader reader = new StreamReader(Request.InputStream))
                        _dataString = reader.ReadToEnd();
                }
                return _dataString;
            }
        }

        private Dictionary<string, string> _dataUrlEncoded = null;
        public Dictionary<string,string> DataUrlEncoded
        {
            get
            {
                if(_dataUrlEncoded == null)
                    _dataUrlEncoded = DataString.Split('&').Select(x => x.Split('=')).ToDictionary(y => y[0], y => (string.IsNullOrEmpty(y[1])) ? null : y[1]);
                return _dataUrlEncoded;
            }
        }
        #endregion
        

        public HttpRequest(HttpListenerContext context)
        {
            Context = context;
            Url = new HttpUrl(this);
        }


        public string GetHeader(string header)
        {
            return Request.Headers.Get(header);
        }


        //Data
        public object GetDataObject(Type type, BodyType body = BodyType.Undefined)
        {
            char firstCharacter = DataString.Trim().FirstOrDefault();
            if (body == BodyType.Undefined)
                switch (firstCharacter)
                {
                    case '{':
                    case '[':
                        body = BodyType.JSON;
                        break;
                    case '<':
                        body = BodyType.XML;
                        break;
                    default:
                        if(DataString.Contains("="))
                            body = BodyType.UrlEncoded;
                        break;
                }

            switch(body)
            {
                case BodyType.JSON:
                    return JsonConvert.DeserializeObject(DataString, type);
                case BodyType.XML:
                    return XmlConvert.DeserializeObject(DataString, type);
                case BodyType.UrlEncoded:
                    object targetObj = Activator.CreateInstance(type);
                    foreach (PropertyInfo info in type.GetPropertiesCached())
                    {
                        if (DataUrlEncoded.ContainsKey(info.Name))
                            info.SetValue(targetObj,
                                ParameterConverter.Static.Parse(info.PropertyType, DataUrlEncoded[info.Name]));
                    }
                    return targetObj;
                case BodyType.Raw:
                    if (type == typeof(string))
                        return DataString;
                    else if (type == typeof(byte[]))
                        return Data;
                    else
                        return null;
                default:
                    return null;
            }
        }
        public T GetDataObject<T>() => (T)GetDataObject(typeof(T));


        //Manipulation
        public void ThrowCode(int code)
        {
            this.Response.StatusCode = code;
            this.Close();
        }

        public void Write(byte[] data)
        {
            Stream s = Response.OutputStream;
            s.Write(data, 0, data.Length);
            s.Flush();
        }
        public void Write(object data, BodyType type = BodyType.JSON)
        {
            byte[] bytes;
            switch(type)
            {
                case BodyType.JSON:
                    bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
                    break;
                case BodyType.XML:
                    bytes = Encoding.UTF8.GetBytes(XmlParser.Serialize(data));
                    break;

                default:
                    throw new NotImplementedException("Given HttpBodyType is not supported");
            }
            Write(bytes);
        }
        public void Write(string data)
        {
            Write(Encoding.UTF8.GetBytes(data));
        }


        public void Redirect(string url)
        {
            Response.Redirect(url);
            Close();
        }


        //Final
        public void Close()
        {
            IsClosed = true;
            Response.Close();
        }

        public void Dispose()
        {
            if(!IsClosed)
            Close();
        }


        
    }
}
