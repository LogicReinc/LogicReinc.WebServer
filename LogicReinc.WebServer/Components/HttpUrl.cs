using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Components
{
    public class HttpUrl
    {
        public HttpRequest request;
        public string[] urlParts;

        public string Domain
        {
            get
            {
                return this.request.Context.Request.Url.Host;
            }
        }

        public string Path
        {
            get
            {
                return this.Request.Context.Request.Url.AbsolutePath.ToLower();
            }
        }


        public HttpUrl(HttpRequest req)
        {
            this.request = req;
        }

        public string GetParameter(string parameterName, bool error = true)
        {
            if (!this.Request.Parameters.ContainsKey(parameterName))
            {
                if (!this.Request.Parameters.ContainsKey(parameterName.ToLower()))
                {
                    if (error)
                        throw new ArgumentException("Parameter " + parameterName + " was not provided");
                    else
                        return "";
                }
                return this.Request.Parameters[parameterName.ToLower()];
            }
            return this.Request.Parameters[parameterName];
        }

        public string GetSubPath(int index, int length)
        {
            List<string> list = this.UrlParts.ToList<string>();
            if (list.Count < index)
            {
                throw new IndexOutOfRangeException("Path too short, Index out of range");
            }
            if (list.Count < (index + length))
            {
                throw new IndexOutOfRangeException("Path too short, Length out of range");
            }
            return ("/" + string.Join("/", list.GetRange(index, length)));
        }

        public bool HasParameter(string parameterName)
        {
            if (!this.Request.Parameters.ContainsKey(parameterName.ToLower()))
            {
                return false;
            }
            return true;
        }


        public HttpRequest Request
        {
            get
            {
                return this.request;
            }
        }

        public string[] UrlParts
        {
            get
            {
                if (this.urlParts == null)
                {
                    string path = this.Path;
                    if (this.Path.Contains("?"))
                    {
                        path = path.Substring(0, path.IndexOf("?"));
                    }
                    path.FirstOrDefault<char>();
                    if (path.FirstOrDefault<char>() == '/')
                    {
                        path = path.Substring(1);
                    }
                    this.urlParts = path.Split(new char[] { '/' });
                }
                return this.urlParts;
            }
        }
        
    }
}
