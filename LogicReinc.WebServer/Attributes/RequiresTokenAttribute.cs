namespace LogicReinc.WebServer.Attributes
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    public class RequiresTokenAttribute : Attribute
    {
        public int LevelRequired { get; private set; }
        public string DataRequired { get; set; }

        public RequiresTokenAttribute(int levelRequirement = 0)
        {
            LevelRequired = levelRequirement;
        }

        public RequiresTokenAttribute(string dataRequirement)
        {
            this.DataRequired = dataRequirement;
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

