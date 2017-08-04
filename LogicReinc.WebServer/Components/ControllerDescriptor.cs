using LogicReinc.API;
using LogicReinc.Extensions;
using LogicReinc.Parsing;
using LogicReinc.WebServer.Attributes;
using LogicReinc.WebServer.Enums;
using LogicReinc.WebServer.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Components
{
    public class ControllerDescriptor
    {
        public Dictionary<string, CallDescriptor> Calls { get; } = new Dictionary<string, CallDescriptor>();

        public Type ControllerType { get; private set; }
        public HttpServer Server { get; private set; }

        public string Name => ControllerType.Name;

        public bool UseWrap { get; set; }

        public ControllerDescriptor(Type t, HttpServer server, bool wrapped = false)
        {
            if (server == null || t == null)
                throw new ArgumentNullException("Type and Server parameter cannot be null");
            UseWrap = wrapped;
            Server = server;
            ControllerType = t;
            List<MethodInfo> ms = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                                        .Where(x => !IgnoreControllerMethodAttribute.HasAttribute(x)).ToList();
            foreach (MethodInfo m in ms)
            {
                CallDescriptor desc = new CallDescriptor(m);
                string name = m.Name.ToLower();
                if (desc.Descriptor != null && !string.IsNullOrEmpty(desc.Descriptor.MethodName))
                    name = desc.Descriptor.MethodName;
                Calls.Add(m.Name.ToLower(), desc);
                if (desc.Descriptor != null && desc.Descriptor.IsDefault && !Calls.ContainsKey(""))
                    Calls.Add("", desc);
            }
        }

        public void Register()
        {
            List<MethodInfo> ms = ControllerType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                                    .Where(x => ControllerRegistrationAttribute.HasAttribute(x)).ToList();
            foreach(MethodInfo m in ms)
            {
                m.Invoke(null, new object[] { Server });
            }
        }

        public bool ExecuteController(string methodName, HttpRequest request)
        {

            string mn = methodName.ToLower();

            if (Calls.ContainsKey(methodName))
            {

                if (request.IsClosed)
                    return true;

                BodyType responseType = Server.DefaultResponseType;

                //Header override
                string accept = request.GetHeader("Accept");
                if(!string.IsNullOrEmpty(accept))
                {
                    switch(accept.ToLower())
                    {
                        case "text/json":
                        case "application/json":
                            responseType = BodyType.JSON;
                            break;
                        case "text/xml":
                        case "application/xml":
                            responseType = BodyType.XML;
                            break;
                        default:
                            //Not supported ResponseType
                            break;
                    }
                }

                try
                {
                    CallDescriptor call = Calls[methodName];
                    
                    //Pre-Controller logic
                    request.Server.PreControllerCheck(request, call);
                    if (request.IsClosed)
                        return true;

                    //With descriptor
                    if (call.Descriptor != null)
                    {
                        responseType = call.Descriptor.ResponseType;
                        if (responseType == BodyType.Undefined)
                            responseType = Server.DefaultResponseType;
                        
                        if (call.Descriptor.HasParameters)
                            foreach (string enforced in call.Descriptor.Parameters)
                                if (!request.Parameters.ContainsKey(enforced))
                                    throw new ArgumentException($"Missing parameter {enforced}");
                    }

                    object result = null;
                    Exception ex = null;

                    try
                    {
                        if (call.TokenRequirements != null)
                        {
                            if (!request.Authenticated || call.TokenRequirements.LevelRequired > request.AuthenticationLevel)
                                throw new ForbiddenException("You don't have permission to access this API method");
                            if (call.TokenRequirements.RequestAttributes != null && call.TokenRequirements.RequestAttributes.Length > 0)
                                foreach (string attr in call.TokenRequirements.RequestAttributes)
                                    if (!request.Attributes.Contains(attr))
                                        throw new ForbiddenException("You don't have permission to access this API method at this moment");
                        }
                        //Caching
                        if (call.CacheHeader != null)
                        {
                            switch(call.CacheHeader.Cache)
                            {
                                case CachingType.Cache:
                                    request.Response.Headers.Add("cache-control", "max-age=" + call.CacheHeader.Validity.ToString());
                                    break;
                                case CachingType.Prevent:
                                    request.Response.Headers.Add("cache-control", "no-cache, no-store, must-revalidate");
                                    request.Response.Headers.Add("Pragma", "no-cache");
                                    request.Response.Headers.Add("Expires", "0");
                                    break;
                            }
                            
                        }

                        //Parse Parameters
                        object[] paras = GetParameters(call, request);

                        //Handling
                        result = call.Info.Invoke(GetInstance(request), paras.ToArray());
                        if (request.DisableAutoHandling)
                            return true;
                    }
                    catch (TargetInvocationException tx)
                    {
                        ex = tx.InnerException;
                    }
                    catch (Exception x)
                    {
                        ex = x;
                    }

                    if(!request.IsClosed && (result != null || ex != null))
                    {

                        Type resultType;
                        if (ex != null)
                        {
                            resultType = typeof(Exception);
                            result = new APIWrap(ex);
                            if (!Server.Debug)
                                ((APIWrap)result).Exception.StackTrace = "";
                            if (responseType == BodyType.Raw || responseType == BodyType.Razor)
                                responseType = Server.DefaultResponseType;
                            HandleResult(Server, request, call, responseType, result, false);
                        }
                        else
                            HandleResult(Server, request, call, responseType, result, UseWrap);
                    }
                }
                catch (Exception ex)
                {
                    if (responseType == BodyType.Razor)
                        responseType = Server.DefaultResponseType;

                    request.Write(new APIWrap(ex), responseType);
                }

                if (!request.IsClosed)
                    request.Close();

                return true;
            }
            else
                return false;
        }

        private static void HandleResult(HttpServer server, HttpRequest request, CallDescriptor call, BodyType responseType, object result, bool useWrap)
        {
            Type resultType = result.GetType();
            if (useWrap && responseType != BodyType.Raw && responseType != BodyType.Razor)
                result = new APIWrap(result);

            if (!server?.IsAllowedResponse(responseType) ?? false)
                throw new Exceptions.ConfigurationException("Requested response type is not allowed");
            switch (responseType)
            {
                case BodyType.Razor:
                    HandleRazor(request, call, result);
                    break;
                case BodyType.Raw:
                    if (resultType == typeof(byte[]))
                    {
                        request.Response.ContentType = "application/octet-stream";
                        request.Write((byte[])result);
                    }
                    else
                    {
                        request.Response.ContentType = "text/plain";
                        request.Write(result.ToString());
                    }
                    break;
                case BodyType.JSON:
                    request.Response.ContentType = "application/json";

                    JsonSerializerSettings settings = call.JsonSerialization?.Response;
                    if (settings != null)
                        request.Write(JsonConvert.SerializeObject(result, settings));
                    else
                        request.Write(JsonConvert.SerializeObject(result));
                    break;
                case BodyType.XML:
                    if (useWrap)
                        XmlParser.AddSubType(typeof(APIWrap), resultType);
                    request.Response.ContentType = "application/xml";
                    request.Write(XmlParser.Serialize(result));
                    break;
                case BodyType.UrlEncoded:
                    throw new NotSupportedException();
                case BodyType.MultipartStream:
                    throw new NotSupportedException();

            }
        }

        //Handling
        private static void HandleRazor(HttpRequest request, CallDescriptor call, object result)
        {
            if (call.Descriptor == null)
                throw new Exception("MethodDescriptor cannot be null for Razor views");

            if (!call.Descriptor.IsRazor)
                throw new ArgumentNullException("RazorView parameter for MethodDescriptor cannot be null");

            string view = call.Descriptor.RazorView;

            if (!request.Server.RazorCache.Contains(view))
                throw new ArgumentException("Given RazorView does not exist");

            request.Response.ContentType = "text/html";
            request.Write(request.Server.RazorCache.Render(view, result));
            request.Close();
        }

        public static ControllerDescriptor GetTemplate<T>(HttpServer server, bool wrapped = true)
        {
            return new ControllerDescriptor(typeof(T), server, wrapped);
        }

        private ControllerBase GetInstance(HttpRequest request)
        {
            ConstructorInfo constructor = ControllerType.GetConstructor(new Type[] { });
            ControllerBase controller = (ControllerBase)constructor.Invoke(new object[] { });
            controller.Init(Server, request);
            return controller;
        }
        
        private object[] GetParameters(CallDescriptor call, HttpRequest request)
        {
            List<object> paras = new List<object>();
            foreach (ParameterDescriptor para in call.Parameters)
            {

                string name = para.Info.Name;
                Type parameterType = para.Info.ParameterType;
                if ((para.Descriptor != null && para.Descriptor.Type == ParameterType.Body) || name == call.Descriptor?.PostParameter)
                {
                    BodyType bType = BodyType.Undefined;

                    if (para.Descriptor != null)
                        bType = para.Descriptor.BodyType;

                    if (call.Descriptor != null && bType == BodyType.Undefined)
                        bType = call.Descriptor.RequestType;

                    if (bType == BodyType.Undefined)
                    {
                        bType = Server.DefaultRequestType;
                        string contentType = request.GetHeader("Content-Type");
                        if(contentType != null)
                        switch(contentType.ToLower())
                        {
                            case "text/json":
                            case "application/json":
                                bType = BodyType.JSON;
                                break;
                            case "text/xml":
                            case "application/xml":
                                bType = BodyType.XML;
                                break;
                            case "application/x-www-form-urlencoded":
                                bType = BodyType.UrlEncoded;
                                break;
                            case "multipart/form-data":
                                bType = BodyType.MultipartStream;
                                break;
                        }
                    }

                    paras.Add(request.GetDataObject(parameterType, bType, call));
                }
                else
                {

                    if (para.Descriptor != null)
                    {
                        if (!string.IsNullOrEmpty(para.Descriptor.Name))
                            name = para.Descriptor.Name;

                        if (!para.Descriptor.Optional && request.Parameters.ContainsKey(name))
                            throw new ArgumentException($"Parameter {name} missing");
                    }

                    string data = null;
                    request.Parameters.TryGetValue(name, out data);

                    if (data != null)
                        paras.Add(ParameterConverter.Static.Parse(parameterType, request.Parameters[name]));
                    else
                        paras.Add((parameterType.IsValueType) ? Activator.CreateInstance(parameterType) : null);
                }
            }
            return paras.ToArray();
        }

        public class CallDescriptor
        {
            public MethodDescriptorAttribute Descriptor { get; set; }
            public CachingAttribute CacheHeader { get; set; }
            public JsonSerializerConfig JsonSerialization { get; set; }

            public RequiresTokenAttribute TokenRequirements { get; set; }
            public MethodInfo Info { get; set; }
            public List<ParameterDescriptor> Parameters { get; set; }

            public CallDescriptor(MethodInfo method)
            {
                Descriptor = MethodDescriptorAttribute.GetDescriptor(method);
                CacheHeader = CachingAttribute.GetAttribute(method);
                TokenRequirements = RequiresTokenAttribute.GetAttribute(method);
                Info = method;
                Parameters = Info.GetParameters().Select(x=>new ParameterDescriptor(x)).ToList();
                JsonSerialization = JsonSerializerAttribute.GetAttribute(method);
            }
        }

        public class ParameterDescriptor
        {
            public ParameterInfo Info { get; set; }
            public ParameterAttribute Descriptor { get; set; }

            public ParameterDescriptor(ParameterInfo info)
            {
                Info = info;
                Descriptor = info.GetCustomAttribute<ParameterAttribute>();
            }
        }

    }
}
