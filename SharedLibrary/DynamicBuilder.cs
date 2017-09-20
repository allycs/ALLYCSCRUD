namespace Dapper
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    ///<summary>
    ///</summary>
    ///<typeparam name="T"></typeparam>
    internal class DynamicBuilder<T>
    {
        private static readonly MethodInfo getValueMethod = typeof(IDataRecord).GetMethod("get_Item", new[] { typeof(int) });

        private static readonly MethodInfo isDBNullMethod = typeof(IDataRecord).GetMethod("IsDBNull", new[] { typeof(int) });

        private Load _handler;

        private DynamicBuilder()
        {
        }

        ///<summary>
        ///</summary>
        ///<param name="dataRecord"></param>
        ///<returns></returns>
        public T Build(IDataRecord dataRecord) => _handler(dataRecord);

        ///<summary>
        ///</summary>
        ///<param name="dataRecord"></param>
        ///<returns></returns>
        public static DynamicBuilder<T> CreateBuilder(IDataRecord dataRecord)
        {
            var dynamicBuilder = new DynamicBuilder<T>();

            var method = new DynamicMethod("DynamicCreate", typeof(T), new[] { typeof(IDataRecord) }, typeof(T), true);
            var generator = method.GetILGenerator();

            var result = generator.DeclareLocal(typeof(T));
            generator.Emit(OpCodes.Newobj, typeof(T).GetConstructor(Type.EmptyTypes));
            generator.Emit(OpCodes.Stloc, result);

            for (var i = 0; i < dataRecord.FieldCount; i++)
            {
                var fieldName = dataRecord.GetName(i);
                var propertyInfo = GetPropOfField(fieldName);
                if (propertyInfo == null)
                    continue;
                bool isNullable = propertyInfo.PropertyType.Name.ToLower().Contains("nullable"); //判断是否为可空类型
                var endIfLabel = generator.DefineLabel();
                if (propertyInfo.GetSetMethod() == null) continue;
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldc_I4, i);
                generator.Emit(OpCodes.Callvirt, isDBNullMethod);
                generator.Emit(OpCodes.Brtrue, endIfLabel);

                generator.Emit(OpCodes.Ldloc, result);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldc_I4, i);
                generator.Emit(OpCodes.Callvirt, getValueMethod);
                //generator.Emit(OpCodes.Unbox_Any, dataRecord.GetFieldType(i));
                var type = dataRecord.GetFieldType(i);
                generator.Emit(OpCodes.Unbox_Any, isNullable ? GetNullableType(type) : type);
                generator.Emit(OpCodes.Callvirt, propertyInfo.GetSetMethod());

                generator.MarkLabel(endIfLabel);
            }

            generator.Emit(OpCodes.Ldloc, result);
            generator.Emit(OpCodes.Ret);

            dynamicBuilder._handler = (Load)method.CreateDelegate(typeof(Load));

            return dynamicBuilder;
        }

        private static Type GetNullableType(Type type)
        {
            Type result = null;
            if (type == typeof(bool))
                result = typeof(bool?);
            if (type == typeof(byte))
                result = typeof(byte?);
            if (type == typeof(DateTime))
                result = typeof(DateTime?);
            if (type == typeof(decimal))
                result = typeof(decimal?);
            if (type == typeof(double))
                result = typeof(double?);
            if (type == typeof(float))
                result = typeof(float?);
            if (type == typeof(Guid))
                result = typeof(Guid?);
            if (type == typeof(short))
                result = typeof(short?);
            if (type == typeof(int))
                result = typeof(int?);
            if (type == typeof(long))
                result = typeof(long?);
            return result;
        }

        #region Nested type: Load

        private delegate T Load(IDataRecord dataRecord);

        #endregion Nested type: Load

        private static Dictionary<Type, Dictionary<string, PropertyInfo>> entityFieldPropMap = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
        private static object mapSync = new object();

        private static PropertyInfo GetPropOfField(string fieldName)
        {
            var entityType = typeof(T);
            var hasMap = entityFieldPropMap.ContainsKey(entityType);
            if (!hasMap)
            {
                lock (mapSync)
                {
                    hasMap = entityFieldPropMap.ContainsKey(entityType);//防止二次扫描
                    if (!hasMap)
                    {
                        var props = entityType.GetProperties();

                        var newMap = new Dictionary<string, PropertyInfo>();

                        var mapTable = props.Where(w => w.SetMethod != null).Select(p => new KeyValuePair<string, PropertyInfo>(TypeHelper.GetFieldName(p), p));//TODO: 是否需要考虑复杂情况
                        foreach (var item in mapTable)
                        {
                            newMap.Add(item.Key, item.Value);
                        }
                        entityFieldPropMap.Add(entityType, newMap);
                    }
                }
            }
            var map = entityFieldPropMap[entityType];
            return map.ContainsKey(fieldName) ? map[fieldName] : null;
        }
    }
}