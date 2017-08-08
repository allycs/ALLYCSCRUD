namespace Dapper
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    internal static class TypeExtension
    {
        //过滤掉不可插入或者修改的复杂对象
        public static bool IsSimpleType(this Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            type = underlyingType ?? type;
            var simpleTypes = new List<Type>
                               {
                                   typeof(byte),
                                   typeof(sbyte),
                                   typeof(short),
                                   typeof(ushort),
                                   typeof(int),
                                   typeof(uint),
                                   typeof(long),
                                   typeof(ulong),
                                   typeof(float),
                                   typeof(double),
                                   typeof(decimal),
                                   typeof(bool),
                                   typeof(string),
                                   typeof(char),
                                   typeof(Guid),
                                   typeof(DateTime),
                                   typeof(DateTimeOffset),
                                   typeof(byte[])
                               };
            return simpleTypes.Contains(type) || type.GetTypeInfo().IsEnum;
        }
    }
}