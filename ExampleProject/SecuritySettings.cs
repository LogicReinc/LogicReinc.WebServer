using LogicReinc.Security;
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

        public bool VerifyUser(string username, string password)
        {
            //Normally database lookup for comparing
            if (username.ToLower() == "admin" && Cryptographics.Hash(password, HashType.Sha256) == ImaginaryAdminPass)
                return true;
            if (ImaginaryOtherUsers.Contains(username.ToLower()))
                return true;
            return false;
        }

        public object GetUserData(string username)
        {
            //Normally database lookup for id
            if (username.ToLower() == "admin")
                return ImaginaryAdminID;

            return Guid.NewGuid().ToString(); //Imaginary User ID
        }

        public int GetTokenLevel(string token)
        {
            //Normally database lookup for security level
            string data = (string)SecurityController<SecSets>.GetTokenData(token);
            if (data == ImaginaryAdminID)
                return 5;
            return 0;
        }
    }
}
