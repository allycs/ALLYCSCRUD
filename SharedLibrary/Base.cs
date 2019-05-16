namespace Dapper
{
    using Microsoft.CSharp.RuntimeBinder;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Dapper.AllycsCRUD
    /// </summary>
    public static partial class AllycsCRUD
    {
        static AllycsCRUD()
        {
            SetDBType(_dialect, _schema, _hasSchema, _isUpToLow);
        }

        private static DBType _dialect;
        private static readonly Populate populate = new Populate();

        private static bool _hasSchema = false;
        private static string _schema;
        /// <summary>
        /// 表名、属性名是否大小写转换（小写时候以下划线分词，类名：AaBb=>aa_bb）
        /// </summary>
        private static bool _isUpToLow = true;

        /// <summary>
        /// 对应数据元素封装方式
        /// </summary>
        private static string _encapsulation;

        /// <summary>
        /// 对应数据库自增主键获取的sql脚本
        /// </summary>
        private static string _getIdentitySql;

        /// <summary>
        /// 对应数据库分页的sql脚本
        /// </summary>
        private static string _getPagedListSql;

        /// <summary>
        /// 存储表名的键值对（字典类型）
        /// </summary>
        private static readonly IDictionary<Type, string> TableNames = new Dictionary<Type, string>();

        /// <summary>
        ///  存储字段名的键值对（字典类型）
        /// </summary>
        private static readonly IDictionary<string, string> ColumnNames = new Dictionary<string, string>();

        private static ITableNameResolver _tableNameResolver = new TableNameResolver();
        private static IColumnNameResolver _columnNameResolver = new ColumnNameResolver();


        /// <summary>
        /// 设置数据库类型
        /// </summary>
        /// <param name="dialect">数据库类型</param>
        /// <param name="isUpToLow">表名、属性名是否区分大小写（小写时候以下划线分词，类名：AaBb=>aa_bb）</param>
        public static void SetDBType(DBType dialect, string schema, bool hasSchema = false, bool isUpToLow = true)
        {
            if (hasSchema)
            {
                _hasSchema = hasSchema;
                _schema = schema;
            }
            _isUpToLow = isUpToLow;
            switch (dialect)
            {
                case DBType.PostgreSQL:
                    _dialect = DBType.PostgreSQL;
                    _encapsulation = "\"{0}\"";
                    _getIdentitySql = string.Format("SELECT LASTVAL() AS id");
                    _getPagedListSql = "SELECT {SelectColumns} FROM {TableName} {WhereClause} ORDER BY {OrderBy} LIMIT {RowsPerPage} OFFSET (({PageNumber}-1) * {RowsPerPage})";
                    break;

                case DBType.SQLite:
                    _dialect = DBType.SQLite;
                    _encapsulation = "\"{0}\"";
                    _getIdentitySql = string.Format("SELECT LAST_INSERT_ROWID() AS id");
                    _getPagedListSql = "SELECT {SelectColumns} FROM {TableName} {WhereClause} ORDER BY {OrderBy} LIMIT {RowsPerPage} OFFSET (({PageNumber}-1) * {RowsPerPage})";
                    break;

                case DBType.MySQL:
                    _dialect = DBType.MySQL;
                    _encapsulation = "`{0}`";
                    _getIdentitySql = string.Format("SELECT LAST_INSERT_ID() AS id");
                    _getPagedListSql = "SELECT {SelectColumns} FROM {TableName} {WhereClause} ORDER BY {OrderBy} LIMIT {Offset},{RowsPerPage}";
                    break;

                default:
                    _dialect = DBType.SQLServer;
                    _encapsulation = "[{0}]";
                    _getIdentitySql = string.Format("SELECT CAST(SCOPE_IDENTITY()  AS BIGINT) AS [id]");
                    _getPagedListSql = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY {OrderBy}) AS PagedNumber, {SelectColumns} FROM {TableName} {WhereClause}) AS u WHERE PagedNUMBER BETWEEN (({PageNumber}-1) * {RowsPerPage} + 1) AND ({PageNumber} * {RowsPerPage})";
                    break;
            }
        }

        /// <summary>
        /// 设置表名解析器
        /// </summary>
        /// <param name="resolver">在请求表名格式时使用解析器</param>
        public static void SetTableNameResolver(ITableNameResolver resolver)
        {
            _tableNameResolver = resolver;
        }

        /// <summary>
        /// 设置列名解析器
        /// </summary>
        /// <param name="resolver">在请求列名格式时使用解析器</param>
        public static void SetColumnNameResolver(IColumnNameResolver resolver)
        {
            _columnNameResolver = resolver;
        }

        /// <summary>
        /// 创建update参数可变字符串（a=1 ， b=2）
        /// </summary>
        /// <param name="entityToUpdate">实体对象</param>
        /// <param name="sb">update语句可变字符串</param>
        private static void BuildUpdateSet(object entityToUpdate, StringBuilder sb)
        {
            var nonIdProps = GetUpdateableProperties(entityToUpdate).ToArray();

            for (var i = 0; i < nonIdProps.Length; i++)
            {
                var property = nonIdProps[i];

                sb.AppendFormat("{0} = @{1}", GetColumnName(property), property.Name);
                if (i < nonIdProps.Length - 1)
                    sb.AppendFormat(", ");
            }
        }

        /// <summary>
        /// 创建select 展示的属性字段不包含带有IgnoreSelect和NotMapped标签的属性
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="props"></param>
        private static void BuildSelect(StringBuilder sb, IEnumerable<PropertyInfo> props)
        {
            var propertyInfos = props as IList<PropertyInfo> ?? props.ToList();
            var addedAny = false;
            for (var i = 0; i < propertyInfos.Count; i++)
            {
                if (propertyInfos.ElementAt(i).GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(IgnoreSelectAttribute).Name || attr.GetType().Name == typeof(NotMappedAttribute).Name)) continue;

                if (addedAny)
                    sb.Append(",");
                sb.Append(GetColumnName(propertyInfos.ElementAt(i)));
                //如果存在自定义属性名，将把原属性值复制到带有该标签的属性上（ColumnAttribute）
                if (propertyInfos.ElementAt(i).GetCustomAttributes(true).SingleOrDefault(attr => attr.GetType().Name == typeof(ColumnAttribute).Name) != null)
                    sb.Append(" as " + Encapsulate(propertyInfos.ElementAt(i).Name));
                addedAny = true;
            }
        }

        private static void BuildWhere(StringBuilder sb, IEnumerable<PropertyInfo> idProps, object sourceEntity, object whereConditions = null)
        {
            var propertyInfos = idProps.ToArray();
            for (var i = 0; i < propertyInfos.Length; i++)
            {
                var useIsNull = false;

                //将泛型属性与源实体属性（可匹配的标签属性）匹配。
                //用于条件限定的匿名对象没有实际的附加相关标签，因此我们需要构建正确的WHERE子句。
                //通过列属性将模型类型转换为数据库列名称
                var propertyToUse = propertyInfos.ElementAt(i);
                var sourceProperties = GetScaffoldableProperties(sourceEntity).ToArray();
                for (var x = 0; x < sourceProperties.Count(); x++)
                {
                    if (sourceProperties.ElementAt(x).Name == propertyInfos.ElementAt(i).Name)
                    {
                        propertyToUse = sourceProperties.ElementAt(x);

                        if (whereConditions != null && propertyInfos.ElementAt(i).CanRead && (propertyInfos.ElementAt(i).GetValue(whereConditions, null) == null || propertyInfos.ElementAt(i).GetValue(whereConditions, null) == DBNull.Value))
                        {
                            useIsNull = true;
                        }
                        break;
                    }
                }
                sb.AppendFormat(
                    useIsNull ? "{0} is null" : "{0} = @{1}",
                    GetColumnName(propertyToUse),
                    propertyInfos.ElementAt(i).Name);

                if (i < propertyInfos.Length - 1)
                    sb.AppendFormat(" and ");
            }
        }

        /// <summary>
        /// 创建INSERT插入值的sql语句包含所有的属性的对应值除了
        /// 以Id命名的属性
        /// 带有Editable(false)标签
        /// 带有Key标签但是不带有Required标签
        /// 带有IgnoreInsert标签
        /// 带有NotMapped标签
        /// </summary>
        /// <param name="entityToInsert"></param>
        /// <param name="sb"></param>
        private static void BuildInsertValues(object entityToInsert, StringBuilder sb)
        {
            var props = GetScaffoldableProperties(entityToInsert).ToArray();
            for (var i = 0; i < props.Count(); i++)
            {
                var property = props.ElementAt(i);
                if (property.PropertyType != typeof(Guid)
                      && property.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(KeyAttribute).Name)
                      && property.GetCustomAttributes(true).All(attr => attr.GetType().Name != typeof(RequiredAttribute).Name))
                    continue;
                if (property.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(IgnoreInsertAttribute).Name)) continue;
                if (property.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(NotMappedAttribute).Name)) continue;
                if (property.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(ReadOnlyAttribute).Name && IsReadOnly(property))) continue;

                if (property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) && property.GetCustomAttributes(true).All(attr => attr.GetType().Name != typeof(RequiredAttribute).Name) && property.PropertyType != typeof(Guid)) continue;

                sb.AppendFormat("@{0}", property.Name);
                if (i < props.Length - 1)
                    sb.Append(", ");
            }
            if (sb.ToString().EndsWith(", "))
                sb.Remove(sb.Length - 2, 2);
        }

        /// <summary>
        /// 创建INSERT语句参数不包含
        /// 带有Editable(false)的标签
        /// 带有Key的标签
        /// 带有NotMapped的标签
        /// 以Id命名的
        /// 属性
        /// </summary>
        /// <param name="entityToInsert"></param>
        /// <param name="sb"></param>
        private static void BuildInsertParameters(object entityToInsert, StringBuilder sb)
        {
            var props = GetScaffoldableProperties(entityToInsert).ToArray();

            for (var i = 0; i < props.Length; i++)
            {
                var property = props.ElementAt(i);
                if (property.PropertyType != typeof(Guid)
                      && property.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(KeyAttribute).Name)
                      && property.GetCustomAttributes(true).All(attr => attr.GetType().Name != typeof(RequiredAttribute).Name))
                    continue;
                if (property.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(IgnoreInsertAttribute).Name)) continue;
                if (property.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(NotMappedAttribute).Name)) continue;

                if (property.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(ReadOnlyAttribute).Name && IsReadOnly(property))) continue;
                if (property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) && property.GetCustomAttributes(true).All(attr => attr.GetType().Name != typeof(RequiredAttribute).Name) && property.PropertyType != typeof(Guid)) continue;

                sb.Append(GetColumnName(property));
                if (i < props.Length - 1)
                    sb.Append(", ");
            }
            if (sb.ToString().EndsWith(", "))
                sb.Remove(sb.Length - 2, 2);
        }

        /// <summary>
        /// 获取实体对象的所有属性名
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static IEnumerable<PropertyInfo> GetAllProperties(object entity)
        {
            if (entity == null) entity = new { };
            return entity.GetType().GetProperties();
        }

        /// <summary>
        /// 获取实体对象的所有属性不包含带有Editable(false)标签的属性
        /// </summary>
        /// <param name="entity">实体对象</param>
        /// <returns>IEnumerable<PropertyInfo></returns>
        private static IEnumerable<PropertyInfo> GetScaffoldableProperties(object entity)
        {
            var props = entity.GetType().GetProperties().Where(p => !p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(EditableAttribute).Name && !IsEditable(p)));
            return props.Where(p => p.PropertyType.IsSimpleType() || IsEditable(p));
        }

        /// <summary>
        /// 如果属性具有AllowEdit标签则返回它的boolean值
        /// </summary>
        /// <param name="pi"></param>
        /// <returns></returns>
        private static bool IsEditable(PropertyInfo pi)
        {
            var attributes = pi.GetCustomAttributes(false);
            if (attributes != null && attributes.Any())
            {
                dynamic write = attributes.FirstOrDefault(x => x.GetType().Name == typeof(EditableAttribute).Name);
                if (write != null)
                {
                    return write.AllowEdit;
                }
            }
            return false;
        }

        /// <summary>
        /// 如果属性具有IsReadOnly标签则返回它的boolean值
        /// </summary>
        /// <param name="pi">属性信息</param>
        /// <returns>IsReadOnly的boolean值</returns>
        private static bool IsReadOnly(PropertyInfo pi)
        {
            var attributes = pi.GetCustomAttributes(false);
            if (attributes != null && attributes.Any())
            {
                dynamic write = attributes.FirstOrDefault(x => x.GetType().Name == typeof(ReadOnlyAttribute).Name);
                if (write != null)
                {
                    return write.IsReadOnly;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取所有属性名不包含
        /// 以Id命名的
        /// 带有Key标签的
        /// 带有ReadOnly标签的
        /// 带有IgnoreInsert标签的
        /// 带有NotMappe标签的
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static IEnumerable<PropertyInfo> GetUpdateableProperties(object entity)
        {
            var updateableProperties = GetScaffoldableProperties(entity);
            //移除Id命名
            updateableProperties = updateableProperties.Where(p => !p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
            //移除带有Key标签
            updateableProperties = updateableProperties.Where(p => p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(KeyAttribute).Name) == false);
            //移除带有readonly标签
            updateableProperties = updateableProperties.Where(p => p.GetCustomAttributes(true).Any(attr => (attr.GetType().Name == typeof(ReadOnlyAttribute).Name) && IsReadOnly(p)) == false);
            //移除带有 IgnoreUpdate 标签
            updateableProperties = updateableProperties.Where(p => p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(IgnoreUpdateAttribute).Name) == false);
            //移除带有NotMappe标签
            updateableProperties = updateableProperties.Where(p => p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(NotMappedAttribute).Name) == false);

            return updateableProperties;
        }

        /// <summary>
        /// 获取所有以Id命名的属性或者带有[Key]标签的属性
        /// 因为INSERT和update 操作传入的是一个实体对象因此该方法是必须的
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static IEnumerable<PropertyInfo> GetIdProperties(object entity)
        {
            var type = entity.GetType();
            return GetIdProperties(type);
        }
        /// <summary>
        /// 获取对象的所有属性
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="entity">实体对象</param>
        /// <returns></returns>
        private static IEnumerable<PropertyInfo> GetAllProperties<T>(T entity) where T : class
        {
            if (entity == null) return new PropertyInfo[0];
            return entity.GetType().GetProperties();
        }

        /// <summary>
        /// 获取T类型所有不带有Editable（false）标签的属性
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns></returns>
        private static IEnumerable<PropertyInfo> GetScaffoldableProperties<T>()
        {
            IEnumerable<PropertyInfo> props = typeof(T).GetProperties();

            props = props.Where(p => p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(EditableAttribute).Name && !IsEditable(p)) == false);


            return props.Where(p => p.PropertyType.IsSimpleType() || IsEditable(p));
        }
        
        /// <summary>
        /// 获取所有属性带有[Key]标签或者以Id命名的属性
        /// 为：Get(id) 和 Delete(id)方法提供获取主键。
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static IEnumerable<PropertyInfo> GetIdProperties(Type type)
        {
            var tp = type.GetProperties().Where(p => p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(KeyAttribute).Name)).ToList();
            return tp.Any() ? tp : type.GetProperties().Where(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 通过对象获取表名
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static string GetTableName(object entity)
        {
            var type = entity.GetType();
            return GetTableName(type);
        }

        /// <summary>
        /// 通过类型获取表名
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static string GetTableName(Type type)
        {

            if (TableNames.TryGetValue(type, out string tableName))
                return tableName;

            tableName = _tableNameResolver.ResolveTableName(type);
            if (_isUpToLow)
                tableName = GetFieldNameByUpperToLower(tableName);
            if (_hasSchema)
                tableName = _schema + "." + tableName;
            TableNames[type] = tableName;

            return tableName;
        }

        private static string GetColumnName(PropertyInfo propertyInfo)
        {
            string columnName, key = string.Format("{0}.{1}", propertyInfo.DeclaringType, propertyInfo.Name);

            if (ColumnNames.TryGetValue(key, out columnName))
                return columnName;

            columnName = _columnNameResolver.ResolveColumnName(propertyInfo);
            if (_isUpToLow)
                columnName = GetFieldNameByUpperToLower(columnName);
            ColumnNames[key] = columnName;

            return columnName;
        }

        /// <summary>
        /// 按照大写转为以"_"关联的小写
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string GetFieldNameByUpperToLower(string name)
        {
            var chars = name.ToCharArray();
            var sb = new StringBuilder();
            sb.Append(chars[0]);
            sb.Append(char.ToLower(chars[1]));
            if (chars.Length > 2)
            {
                for (int i = 2; i < chars.Length; i++)
                {
                    var c = chars[i];
                    if (char.IsUpper(c) && !char.IsDigit(c))
                    {
                        sb.Append("_").Append(char.ToLower(c));
                    }
                    else
                    {
                        sb.Append(char.ToLower(c));
                    }
                }
            }
            return sb.ToString();
        }

        private static string Encapsulate(string databaseword)
        {
            return string.Format(_encapsulation, databaseword);
        }

        /// <summary>
        /// 基于当前日期时间生成一个GUID
        /// http://stackoverflow.com/questions/1752004/sequential-guid-generator-c-sharp
        /// </summary>
        /// <returns></returns>
        public static Guid SequentialGuid()
        {
            var tempGuid = Guid.NewGuid();
            var bytes = tempGuid.ToByteArray();
            var time = DateTime.Now;
            bytes[3] = (byte)time.Year;
            bytes[2] = (byte)time.Month;
            bytes[1] = (byte)time.Day;
            bytes[0] = (byte)time.Hour;
            bytes[5] = (byte)time.Minute;
            bytes[4] = (byte)time.Second;
            return new Guid(bytes);
        }

        public interface ITableNameResolver
        {
            string ResolveTableName(Type type);
        }

        public interface IColumnNameResolver
        {
            string ResolveColumnName(PropertyInfo propertyInfo);
        }

        public class TableNameResolver : ITableNameResolver
        {
            public virtual string ResolveTableName(Type type)
            {
                var tableName = Encapsulate(type.Name);

                var tableattr = type.GetTypeInfo().GetCustomAttributes(true).SingleOrDefault(attr => attr.GetType().Name == typeof(TableAttribute).Name) as dynamic;
                if (tableattr != null)
                {
                    tableName = Encapsulate(tableattr.Name);
                    try
                    {
                        if (!String.IsNullOrEmpty(tableattr.Schema))
                        {
                            string schemaName = Encapsulate(tableattr.Schema);
                            tableName = String.Format("{0}.{1}", schemaName, tableName);
                        }
                    }
                    catch (RuntimeBinderException)
                    {
                        //Schema doesn't exist on this attribute.
                    }
                }

                return tableName;
            }
        }

        public class ColumnNameResolver : IColumnNameResolver
        {
            public virtual string ResolveColumnName(PropertyInfo propertyInfo)
            {
                var columnName = Encapsulate(propertyInfo.Name);

                var columnattr = propertyInfo.GetCustomAttributes(true).SingleOrDefault(attr => attr.GetType().Name == typeof(ColumnAttribute).Name) as dynamic;
                if (columnattr != null)
                {
                    columnName = Encapsulate(columnattr.Name);
                    if (Debugger.IsAttached)
                        Debug.WriteLine(String.Format("Column name for type overridden FROM {0} to {1}", propertyInfo.Name, columnName));
                }
                return columnName;
            }
        }
    }
}