using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Attributes
{
    public class SyncControllerAttribute : Attribute
    {

        public SyncControllerAttribute()
        {

        }

        public static bool IsSync(PropertyInfo info)
        {
            return info.GetCustomAttribute<SyncControllerAttribute>() != null;
        }
    }
}
