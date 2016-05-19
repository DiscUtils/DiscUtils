
namespace System.Reflection
{
    internal static class TypeExtensions
    {
#if NETCORE
        public static PropertyInfo[] GetProperties(this Type type)
        {
            return type.GetTypeInfo().GetProperties();
        }

        public static PropertyInfo[] GetProperties(this Type type, BindingFlags bindingAttr)
        {
            return type.GetTypeInfo().GetProperties(bindingAttr);
        }

        public static FieldInfo[] GetFields(this Type type)
        {
            return type.GetTypeInfo().GetFields();
        }
#endif

        public static Assembly GetAssembly(this Type type)
        {
#if NETCORE
            return type.GetTypeInfo().Assembly;
#else
            return type.Assembly;
#endif
        }

        public static bool GetIsEnum(this Type type)
        {
#if NETCORE
            return type.GetTypeInfo().IsEnum;
#else
            return type.IsEnum;
#endif
        }
    }
}