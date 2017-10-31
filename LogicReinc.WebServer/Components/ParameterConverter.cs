using LogicReinc.Attributes;
using LogicReinc.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Components
{
    public class ParameterConverter : StringParser
    {
        public new static ParameterConverter Static { get; } = new ParameterConverter();

        [Type(typeof(DateTime))]
        public object ToDateTime(string str) => DateTime.Parse(str);
    }
}
