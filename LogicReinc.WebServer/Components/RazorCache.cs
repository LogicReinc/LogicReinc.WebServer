using RazorTemplates.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Components
{
    public class RazorCache
    {
        Dictionary<string, ITemplate<dynamic>> templates = new Dictionary<string, ITemplate<dynamic>>();

        public bool Contains(string name)
        {
            return templates.ContainsKey(name.ToLower());
        }

        public void AddTemplate(string name, string html)
        {
            try
            {
                templates.Add(name.ToLower(), Template.Compile(html));
            }
            catch(Exception ex)
            {
                throw;
            }
        }

        public string Render(string template, object obj)
        {
            template = template.ToLower();
            if (templates.ContainsKey(template))
                return templates[template].Render(obj);
            throw new KeyNotFoundException("Template does not exist");
        }

        public void Clear()
        {
            templates.Clear();
        }
    }
}
