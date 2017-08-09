using LogicReinc.WebServer.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Attributes
{
    public class  MethodDescriptorAttribute : Attribute
    {
   
        public List<string> Parameters { get; private set; } = new List<string>();
        public bool HasParameters => Parameters.Count > 0;

        public string MethodName { get; set; } = null;

        public bool IsDefault { get; set; }

        public string PostParameter { get; set; } = "";
        public bool HasPostParameter => !string.IsNullOrEmpty(PostParameter);

        public string RazorView { get; set; } = "";
        public bool IsRazor => RazorView != "";

        public BodyType RequestType { get; set; }
        public BodyType ResponseType { get; set; }

        //string postParameter = "", string razorView = "", string parameters = "", BodyType bodyType = BodyType.Undefined, BodyType responseType = BodyType.Undefined
        public MethodDescriptorAttribute(string[] parameters = null)
        {
            if (parameters != null)
                Parameters = parameters.ToList();
        }

        public static MethodDescriptorAttribute GetDescriptor(MethodInfo method)
        {
            return method.GetCustomAttribute<MethodDescriptorAttribute>();
        }
    }
}
