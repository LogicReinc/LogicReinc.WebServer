using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Attributes
{
    public class IgnoreControllerMethodAttribute : Attribute
    {
        public static bool HasAttribute(MethodInfo info)
        {
            return info.GetCustomAttribute<IgnoreControllerMethodAttribute>() != null;
        }
    }
}
