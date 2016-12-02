using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Attributes
{
    public class ControllerRegistrationAttribute : Attribute
    {
        public static bool HasAttribute(MethodInfo method)
        {
            return method.GetCustomAttribute<ControllerRegistrationAttribute>() != null;
        }
    }
}
