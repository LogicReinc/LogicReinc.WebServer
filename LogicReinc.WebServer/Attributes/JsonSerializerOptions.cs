using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Reflection;

namespace LogicReinc.WebServer.Attributes
{
    public class JsonSerializerConfig
    {
        public JsonSerializerSettings Request { get; set; }
        public JsonSerializerSettings Response { get; set; }
    }

    public class JsonSerializerAttribute : Attribute
    {
        public TypeNameHandling? TypeHandling { get; set; }
        public PreserveReferencesHandling? ReferenceHandling { get; set; }
       

        

        public JsonSerializerSettings ToSettings()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            if (TypeHandling != null)
                settings.TypeNameHandling = TypeHandling.Value;
            if (ReferenceHandling != null)
                settings.PreserveReferencesHandling = ReferenceHandling.Value;

            return settings;
        }

        public static JsonSerializerConfig GetAttribute(MethodInfo method)
        {
            return new JsonSerializerConfig(){

                Request = method.GetCustomAttribute<JsonSerializerAttribute>()?.ToSettings(),
                Response = method.GetCustomAttribute<JsonResponseSerializerAttribute>()?.ToSettings()
            };
        }
    }

    public class JsonRequestSerializerAttribute : JsonSerializerAttribute
    {

        public JsonRequestSerializerAttribute(TypeNameHandling typeHandling)
        {
            TypeHandling = typeHandling;
        }

        public JsonRequestSerializerAttribute(PreserveReferencesHandling referenceHandling)
        {
            ReferenceHandling = referenceHandling;
        }

        public JsonRequestSerializerAttribute(TypeNameHandling typeHandling, PreserveReferencesHandling referenceHandling)
        {
            TypeHandling = typeHandling;
            ReferenceHandling = referenceHandling;
        }
    }

    public class JsonResponseSerializerAttribute : JsonSerializerAttribute
    {

        public JsonResponseSerializerAttribute(TypeNameHandling typeHandling)
        {
            TypeHandling = typeHandling;
        }

        public JsonResponseSerializerAttribute(PreserveReferencesHandling referenceHandling)
        {
            ReferenceHandling = referenceHandling;
        }

        public JsonResponseSerializerAttribute(TypeNameHandling typeHandling, PreserveReferencesHandling referenceHandling)
        {
            TypeHandling = typeHandling;
            ReferenceHandling = referenceHandling;
        }
    }
}
