using LogicReinc.Security;
using LogicReinc.Security.TokenSystem;
using LogicReinc.WebServer;
using LogicReinc.WebServer.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleProject
{
    public class SecSets : ISecuritySettings
    {
        private static string ImaginaryAdminID = Guid.NewGuid().ToString();
        private static string ImaginaryAdminPass = Cryptographics.Hash("testtesttest", HashType.Sha256);
        private static string[] ImaginaryOtherUsers = new string[] { "user1", "user2", "user3" };

        public bool HandleHashing => false;
        public string UniqueName => "ExampleProject";

        public bool HasUserData => true;
        public bool SendUserData => true;

        public bool VerifyUser(string username, string password, out object userData)
        {
            //Normally database lookup for comparing
            if ((username.ToLower() == "admin" && Cryptographics.Hash(password, HashType.Sha256) == ImaginaryAdminPass) || ImaginaryOtherUsers.Contains(username.ToLower()))
            {
                if (username.ToLower() == "admin")
                    userData = ImaginaryAdminID;
                else 
                    userData = Guid.NewGuid().ToString();
                return true;
            }

            userData = null;
            return false;
        }

        public int GetTokenLevel(Token token)
        {
            //Normally database lookup for security level
            string data = (string)token.Data;
            if (data == ImaginaryAdminID)
                return 5;
            return 0;
        }
    }
}
