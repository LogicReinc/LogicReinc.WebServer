using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Enums
{
    public enum BodyType
    {
        Undefined,
        JSON,
        XML,
        Raw,
        UrlEncoded,
        Razor,
        MultipartStream
    }
}

