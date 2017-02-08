using LogicReinc.Extensions;
using LogicReinc.Languages.Javascript.Builder;
using LogicReinc.WebServer;
using LogicReinc.WebServer.Attributes;
using LogicReinc.WebServer.Components;
using LogicReinc.WebServer.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static LogicReinc.WebServer.Components.ControllerDescriptor;
using static LogicReinc.WebServer.Components.RoutingCollection;

namespace LogicReinc.WebSercer.Controllers
{
    public class SyncController : ControllerBase
    {
        public static string GeneratedSync { get; private set; }
        public static Dictionary<int, string> GeneratedSyncAuthorized { get; private set; } = new Dictionary<int, string>();
        public static string APIBaseUrl { get; set; } = "";

        [IgnoreControllerMethod]
        public static JSBFunction CreateLinkFunction(string baseApiPath, string controller, MethodInfo info, bool useName)
        {
            List<ParameterInfo> paras = info.GetParameters().Where(x => !BodyAttribute.HasAttribute(x)).ToList();

            ParameterInfo bodyParameter = info.GetParameters().FirstOrDefault(x => BodyAttribute.HasAttribute(x));
            string bodyParaName = (bodyParameter != null) ? bodyParameter.Name : "";

            

            List<string> stringParas = paras.Select(x => x.Name).ToList();

            MethodDescriptorAttribute descriptor = MethodDescriptorAttribute.GetDescriptor(info);
            if (descriptor != null)
            {
                stringParas = new List<string>();

                foreach (string para in descriptor.Parameters)
                    stringParas.Add(para);

                if (descriptor.HasPostParameter)
                    bodyParaName = descriptor.PostParameter;
            }
            
            if (RequiresTokenAttribute.HasAttribute(info))
                stringParas.Add("t");

            JSBuilder code = new JSBuilder();

            StringBuilder paraString = new StringBuilder();
            for (int i = 0; i < stringParas.Count; i++)
            {
                if (i == 0)
                    paraString.Append("?");
                else
                    paraString.Append("&");
                paraString.Append(stringParas[i] + "=' + " + stringParas[i] + " + '");
            }

            if (RequiresTokenAttribute.HasAttribute(info))
                code.AddCode("if(!t) t = Sync.Token;");

            if (APIBaseUrl == "")
            {
                if (!string.IsNullOrEmpty(bodyParaName))
                    code.AddCode($"SyncPostJson('{controller}/{info.Name}{paraString.ToString()}', {"JSON.stringify(" + bodyParaName + ")"}, callback);");
                else
                    code.AddCode($"SyncGetJson('{controller}/{info.Name}{paraString.ToString()}', callback);");
            }
            else
            {
                if (!string.IsNullOrEmpty(bodyParaName))
                    code.AddCode($"SyncPostJson('{APIBaseUrl}/{controller}/{info.Name}{paraString.ToString()}', JSON.stringify({bodyParaName}), callback);");
                else
                    code.AddCode($"SyncGetJson('{APIBaseUrl}/{controller}/{info.Name}{paraString.ToString()}', callback);");
            }
            if (!string.IsNullOrEmpty(bodyParaName))
            {
                if (stringParas.LastOrDefault() == "t")
                    stringParas.Insert(stringParas.Count - 1, bodyParaName);
                else
                    stringParas.Add(bodyParaName);
            }
            if (stringParas.LastOrDefault() == "t")
                stringParas.Insert(stringParas.Count - 1, "callback");
            else
                stringParas.Add("callback");

            JSBFunction func = new JSBFunction(((useName) ? info.Name : null), code, stringParas.ToArray());

            return func;
        }

        protected virtual List<ControllerRoute> GetControllers() => Server.Routing.GetControllers().Where(x => x.Controller.ControllerType != typeof(SyncController)).ToList();

        [Caching(CachingType.Prevent)]
        [MethodDescriptorAttribute(IsDefault = true, ResponseType = BodyType.Raw)]
        public string Sync()
        {
            bool isUpdate = false;
            if (Request.Parameters.ContainsKey("update"))
                bool.TryParse(Request.Parameters["update"], out isUpdate);


            if ((Authenticated && !GeneratedSyncAuthorized.ContainsKey(AuthenticationLevel)) || (!Authenticated && string.IsNullOrEmpty(GeneratedSync)))
            {
                JSBObject jsControllers = new JSBObject();

                foreach (ControllerRoute tkv in GetControllers())
                {
                    string controllerPath = tkv.Path;
                    //Console.WriteLine(controllerPath);
                    ControllerDescriptor template = tkv.Controller;

                    JSBObject jsController = new JSBObject();

                    foreach (KeyValuePair<string, CallDescriptor> mkv in template.Calls.Where(x=>!IgnoreSyncAttribute.HasAttribute(x.Value.Info)))
                    {
                        MethodInfo method = mkv.Value.Info;

                        RequiresTokenAttribute requiresVerify = RequiresTokenAttribute.GetAttribute(method);

                        if (requiresVerify == null || (requiresVerify != null && Authenticated && requiresVerify.LevelRequired <= AuthenticationLevel))
                        {
                            string typeName = template.ControllerType.Name;
                            string controllerName = typeName.Replace("Controller", "").Replace("`1", "");
                            

                            JSBFunction func = CreateLinkFunction(APIBaseUrl, controllerPath, method, false);
                            
                            if(!jsController.Properties.ContainsKey(method.Name))
                                jsController.AddFunction(method.Name, func);
                        }
                    }
                    if(jsController.Properties.Any())
                        jsControllers.AddObject(template.ControllerType.Name.Replace("Controller", "").Replace("`1", ""), jsController);
                }

                JSBuilder js = new JSBuilder().AssignVariable("Sync", jsControllers);

                if (!isUpdate)
                    js.DefineFunction("SyncGetJson", @"
    var ajax = new XMLHttpRequest();
    ajax.onreadystatechange = function () {
        if (ajax.readyState == 4 && ajax.status == 200)
            /*callback(JSON.parse(ajax.responseText))*/
            callback(ResolveReferences(ajax.responseText))
    };
    ajax.open('GET', url, true);
    ajax.setRequestHeader('Accept', 'application/json');
        ajax.send();
", "url", "callback")
                    .DefineFunction("SyncPostJson", @"
    var ajax = new XMLHttpRequest();
    ajax.onreadystatechange = function () {
        if (ajax.readyState == 4 && ajax.status == 200) {
            /*callback(JSON.parse(ajax.responseText))*/
            callback(ResolveReferences(ajax.responseText))
        }
    };
    ajax.open('POST', url, true);
    ajax.send(data);
", "url", "data", "callback")
                    .DefineFunction("ResolveReferences", @"if (typeof json === 'string')
        json = JSON.parse(json);

    var byid = {};
    var refs = [];
    json = (function recurse(obj, prop, parent) {
        if (typeof obj !== 'object' || !obj)
            return obj;
        if (Object.prototype.toString.call(obj) === '[object Array]') {
            for (var i = 0; i < obj.length; i++)
                if (typeof obj[i] !== 'object' || !obj[i])
                    continue;
                else if (""$ref "" in obj[i])
                    obj[i] = recurse(obj[i], i, obj);
                else
                    obj[i] = recurse(obj[i], prop, obj);
                return obj;
            }
            if (""$ref"" in obj) {
                var ref = obj.$ref;
                if (ref in byid)
                return byid[ref];
                refs.push([parent, prop, ref]);
                return;
            } else if (""$id"" in obj) {
                var id = obj.$id;
                delete obj.$id;
                if (""$values"" in obj)
                obj = obj.$values.map(recurse);
            else
                for (var prop in obj)
                    obj[prop] = recurse(obj[prop], prop, obj);
                byid[id] = obj;
            }
            return obj;
        })(json);

    for (var i = 0; i<refs.length; i++) {
        var ref = refs[i];
        ref[0][ref[1]] = byid[ref[2]];
    }
    return json;", "json");


                if (Authenticated)
                    GeneratedSyncAuthorized.Add(AuthenticationLevel, js.BuildCode());
                else
                    GeneratedSync = js.BuildCode();
            }

            Request.Response.ContentType = "application/javascript";

            string addition = "";
            if (Authenticated)
                addition += $"Sync.Token = '{Token.UrlEncode()}';";

            if (isUpdate) 
                return ((Authenticated) ? GeneratedSyncAuthorized[AuthenticationLevel] : GeneratedSync) + "\n" + addition;
            else
                return ((Authenticated) ? "var " + GeneratedSyncAuthorized[AuthenticationLevel] : "var " + GeneratedSync) +"\n" + addition;
        }

    }
}
