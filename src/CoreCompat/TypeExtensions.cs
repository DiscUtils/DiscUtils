#if NETCORE
namespace System.Reflection
{
    internal static class TypeExtensions
    {
        public static PropertyInfo[] GetProperties(this Type type)
        {
            return type.GetTypeInfo().GetProperties();
        }

        public static PropertyInfo[] GetProperties(this Type type, BindingFlags bindingAttr)
        {
            return type.GetTypeInfo().GetProperties(bindingAttr);
        }
    }
}
#endif