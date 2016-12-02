using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Attributes
{
    public class RazorViewAttribute : Attribute
    {
        public string View { get; private set; }
        public bool FromCache { get; private set; }

        public RazorViewAttribute(string view, bool fromCache)
        {
            this.View = view;
            FromCache = fromCache;
        }
    }
}
