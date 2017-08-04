namespace LogicReinc.WebServer.Attributes
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    public class RequiresTokenAttribute : Attribute
    {
        public int LevelRequired { get; private set; }
        public string DataRequired { get; set; }
        public string[] RequestAttributes { get; private set; }

        public RequiresTokenAttribute(int levelRequirement = 0)
        {
            LevelRequired = levelRequirement;
        }

        public RequiresTokenAttribute(string dataRequirement)
        {
            this.DataRequired = dataRequirement;
        }

        public RequiresTokenAttribute(int level, params string[] requiredTokens)
        {
            LevelRequired = level;
            RequestAttributes = requiredTokens;
        }

        public RequiresTokenAttribute(string[] requiresTokens)
        {
            LevelRequired = 0;
            RequestAttributes = requiresTokens;
        }

        public static RequiresTokenAttribute GetAttribute(MethodInfo info)
        {
            return info.GetCustomAttribute<RequiresTokenAttribute>();
        }

        public static bool HasAttribute(MethodInfo info)
        {
            return (info.GetCustomAttribute<RequiresTokenAttribute>() != null);
        }
    }
}

