using LogicReinc.WebServer.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Attributes
{
    /// <summary>
    /// Parameter Descriptor
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ParameterAttribute : Attribute
    {
        public ParameterType Type { get; set; }
        public BodyType BodyType { get; set; }

        public string Name { get; set; }
        public bool Optional { get; set; }

        public ParameterAttribute(string name, bool optional = false)
        {
            Optional = optional;
            Type = ParameterType.Url;
            Name = name;
        }

        public ParameterAttribute(BodyType body, bool optional = false)
        {
            Optional = optional;
            Type = ParameterType.Body;
            BodyType = body;
        }
    }
    
    public class BodyAttribute : ParameterAttribute
    {
        public BodyAttribute(BodyType type = BodyType.Undefined) : base(type) { }

        public static bool HasAttribute(ParameterInfo info)
        {
            return info.GetCustomAttribute<BodyAttribute>() != null;
        }
    }
}
