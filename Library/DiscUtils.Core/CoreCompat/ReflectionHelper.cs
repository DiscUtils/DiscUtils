using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DiscUtils.CoreCompat
{
    internal static class ReflectionHelper
    {
        public static bool IsEnum(Type type)
        {
            return type.IsEnum;
        }

        public static Attribute GetCustomAttribute(PropertyInfo property, Type attributeType)
        {
            return Attribute.GetCustomAttribute(property, attributeType);
        }

        public static Attribute GetCustomAttribute(PropertyInfo property, Type attributeType, bool inherit)
        {
            return Attribute.GetCustomAttribute(property, attributeType, inherit);
        }

        public static Attribute GetCustomAttribute(FieldInfo field, Type attributeType)
        {
            return Attribute.GetCustomAttribute(field, attributeType);
        }

        public static Attribute GetCustomAttribute(Type type, Type attributeType)
        {
            return Attribute.GetCustomAttribute(type, attributeType);
        }

        public static Attribute GetCustomAttribute(Type type, Type attributeType, bool inherit)
        {
            return Attribute.GetCustomAttribute(type, attributeType);
        }

        public static IEnumerable<Attribute> GetCustomAttributes(Type type, Type attributeType, bool inherit)
        {
            return Attribute.GetCustomAttributes(type, attributeType);
        }

        public static Assembly GetAssembly(Type type)
        {
            return type.Assembly;
        }

        public static int SizeOf<T>()
        {
            return Marshal.SizeOf(typeof(T));
        }
    }
}