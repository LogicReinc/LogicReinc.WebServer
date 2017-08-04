using LogicReinc.API;
using LogicReinc.Security;
using LogicReinc.Security.TokenSystem;
using LogicReinc.WebServer.Attributes;
using LogicReinc.WebServer.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Controllers
{
    public class SecurityController<SecSettings> : ControllerBase where SecSettings : ISecuritySettings
    {
        private static string unique = Guid.NewGuid().ToString();
        private static TokenSystem tokenSysten = new TokenSystem();// Settings.TokenDuration);

        private static Dictionary<Type, SecSettings> settings = new Dictionary<Type, SecSettings>();
        public static SecSettings Settings
        {
            get
            {
                Type secsetType = typeof(SecSettings);
                if (!settings.ContainsKey(secsetType))
                    settings.Add(secsetType, Activator.CreateInstance<SecSettings>());
                return settings[secsetType];
            }
        }
        
        private static string GenerateUnique(HttpRequest request)
        {
            
            string addr = request.RemoteAddress;
            if (addr == "::1")
                addr = "127.0.0.1";
            return unique + addr.Replace(".", "_");
        }

        public static Token GetToken(HttpServer server, HttpRequest request)
        {
            SecurityController<SecSettings> ctrl = new SecurityController<SecSettings>();
            ctrl.Init(server, request);

            return ctrl.GetToken();
        }

        public static Token GetToken(HttpRequest request, SecurityUser user)
        {
            string username = user.Username;
            string pass = user.Password;

           
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(pass))
                throw new ArgumentException("Username or Password was not valid");


            object userData = null;

            if (!Settings.VerifyUser(username, pass, out userData))
                throw new InvalidCredentialsException("Credentials are not correct");


            //if (Settings.HasUserData)
            //    userData = Settings.GetUserData(username);
            return tokenSysten.CreateToken(GenerateUnique(request), userData);
        }

        public static object GetTokenData(string token) => tokenSysten.GetTokenData(token);

        public static int GetTokenLevel(Token token)
        {
            //string data = GetTokenData(token);
            return Settings.GetTokenLevel(token);
        }

        //APICALL
        [MethodDescriptorAttribute(null, PostParameter = "user")]
        public virtual Token GetToken()
        {
            SecurityUser user = Request.GetDataObject<SecurityUser>();
            Token token = SecurityController<SecSettings>.GetToken(Request, user);
            if (!Settings.SendUserData)
                return new Token()
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    Duration = token.Duration
                };
            else
                return token;
        }

        //APICALL
        [MethodDescriptorAttribute(new string[] { "t", "rt" })]
        public virtual Token RefreshToken()
        {
            string tPara = base.Request.Url.GetParameter("t");
            string rtPara = base.Request.Url.GetParameter("rt");

            string ipKey = GenerateUnique(Request);
            return tokenSysten.UseRefreshToken(ipKey, tPara, rtPara);
        }

        public static bool VerifyToken(HttpRequest request, string token)
        {
            string ipKey = GenerateUnique(request);
            return tokenSysten.VerifyToken(ipKey, token);
        }


        [ControllerRegistration]
        private static void Register(HttpServer server)
        {
            //Ensure unsubscribed
            server.OnRequest -= HandleSecurity;
            server.OnRequest += HandleSecurity;
        }
        private static void HandleSecurity(HttpRequest request)
        {
            if(request.Parameters.ContainsKey("t"))
            {
                try
                {
                    string token = request.Parameters["t"];

                    request.Token = token;
                    request.TokenData = tokenSysten.GetTokenData(token);
                    if (VerifyToken(request, token))
                    {
                        request.Authenticated = true;

                        Token tk = tokenSysten.GetToken(token);
                        request.AuthenticationLevel = GetTokenLevel(tk);
                        Settings.PrepareRequest(tk, request);
                    }
                    else
                    {
                        request.Response.ContentType = request.Server.DefaultResponseTypeMime;
                        request.Write(new APIWrap(new InvalidTokenException()), request.Server.DefaultResponseType);
                        request.Close();
                    }
                }
                catch(Exception ex)
                {

                }
            }
        }


        

    }
    public class SecurityUser
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public interface ISecuritySettings
    {
        bool HasUserData { get; }
        bool SendUserData { get; }
        bool IsIPBoundToken { get; }
        int TokenDuration { get; }

        bool VerifyUser(string username, string password, out object userData);
        int GetTokenLevel(Token token);

        void PrepareRequest(Token token, HttpRequest request);
    }
}

