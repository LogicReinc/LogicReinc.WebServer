using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Attributes
{
    public enum CachingType
    {
        None,
        Prevent,
        Cache
    }

    public class CachingAttribute : Attribute
    {

        public CachingType Cache { get; private set; }
        public int Validity { get; private set; }

        public CachingAttribute(CachingType cacheType, int validity = 0)
        {
            Cache = cacheType;
            Validity = validity;
        }

        public static CachingAttribute GetAttribute(MethodInfo info)
        {
            return info.GetCustomAttribute<CachingAttribute>();
        }
    }
}
