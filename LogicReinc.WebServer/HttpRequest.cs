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
using static LogicReinc.WebServer.Components.ControllerDescriptor;

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
        
        //Properties (Utility)
        public bool AcceptRange => GetHeader("Accept-Ranges") == "bytes";


        //Constructor
        public HttpRequest(HttpListenerContext context)
        {
            Context = context;
            Url = new HttpUrl(this);
        }


        //Utility
        public string GetHeader(string header)
        {
            return Request.Headers.Get(header);
        }


        //Data
        public object GetDataObject(Type type, BodyType body = BodyType.Undefined, CallDescriptor call = null)
        {
            if (body == BodyType.Undefined)
            {
                char firstCharacter = DataString.Trim().FirstOrDefault();
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
                        if (DataString.Contains("="))
                            body = BodyType.UrlEncoded;
                        break;
                }
            }

            switch(body)
            {
                case BodyType.JSON:
                    JsonSerializerSettings settings = call?.JsonSerialization?.Request;
                    if (settings != null)
                        return JsonConvert.DeserializeObject(DataString, type, settings);
                    else
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
                case BodyType.MultipartStream:
                    return new MultiPartStream(DataStream);
                default:
                    return null;
            }
        }
        public T GetDataObject<T>() => (T)GetDataObject(typeof(T));


        //Manipulation
        public void SetCode(int code)
        {
            this.Response.StatusCode = code;
        }

        public void ThrowCode(int code)
        {
            this.Response.StatusCode = code;
            this.Close();
        }

        public void ThrowCode(int code, byte[] data)
        {
            this.Response.StatusCode = code;
            this.Response.OutputStream.Write(data, 0, data.Length);
            this.Close();
        }
        
        public void ThrowCode(int code, string data)
        {
            this.Response.StatusCode = code;
            byte[] sData = Encoding.UTF8.GetBytes(data);
            this.Response.OutputStream.Write(sData, 0, sData.Length);
            this.Close();
        }

        public void Write(byte[] data)
        {
            Stream s = Response.OutputStream;
            s.Write(data, 0, data.Length);
            s.Flush();
        }
        public void Write(byte[] data, int length)
        {
            Response.OutputStream.Write(data, 0, length);
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


        public void Stream(Stream stream)
        {
            Response.StatusCode = 200;

            Response.AddHeader("Accept-Ranges", "bytes");

            string range = GetHeader("Range") ?? "";
            if (!range.StartsWith("bytes="))
            {
                Close();
                return;
            }
            range = range.Split('=')[1];
            string[] rangeParts = range.Split('-');

            long start;
            long end;
            if (!long.TryParse(rangeParts[0], out start))
            {
                ThrowCode(400);
                return;
            }
            if (rangeParts.Length == 1 || !long.TryParse(rangeParts[1], out end))
                end = stream.Length - 1;

            long toRead = end - start + 1;
            const long bufferSize = 4096;
            Response.ContentLength64 = toRead;
            Response.KeepAlive = true;
            Response.SendChunked = false;
            Response.Headers.Add("Content-Range", $"bytes {start}-{start + toRead - 1}/{stream.Length}");
            Response.StatusCode = 206;
            byte[] buffer = new byte[bufferSize];
            int read = 0;
            long readTotal = 0;
            try
            {
                if(start > 0)
                    stream.Seek(start, SeekOrigin.Begin);
                using(Stream str = Response.OutputStream)
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0 && readTotal < toRead)
                {
                    //Write(buffer, read);
                    Write(buffer, read);
                    readTotal += read;
                }

                Response.OutputStream.Flush();
            }
            catch(Exception ex)
            {
                //Connection closed
                Console.WriteLine(ex.Message);
            }
            Close();
        }

        public void Redirect(string url)
        {
            Response.Redirect(url);
            Close();
        }

        

        //Final
        public void Close()
        {
            Response.OutputStream.Flush();
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
