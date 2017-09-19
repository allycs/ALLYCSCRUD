namespace Dapper
{
    using Dapper;
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
    public static class AllycsCRUD
    {
        static AllycsCRUD()
        {
            SetDialect(_dialect, _isUpToLow);
        }

        private static Dialect _dialect = Dialect.PostgreSQL;
        private static readonly Populate populate = new Populate();

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

        private static readonly IDictionary<Type, string> TableNames = new Dictionary<Type, string>();
        private static readonly IDictionary<string, string> ColumnNames = new Dictionary<string, string>();

        private static ITableNameResolver _tableNameResolver = new TableNameResolver();
        private static IColumnNameResolver _columnNameResolver = new ColumnNameResolver();

        /// <summary>
        /// 支持数据库类型
        /// </summary>
        public enum Dialect
        {
            /// <summary>
            /// MSSQL
            /// </summary>
            SQLServer,
            /// <summary>
            /// PostgresSQL
            /// </summary>
            PostgreSQL,
            /// <summary>
            /// SQLite
            /// </summary>
            SQLite,
            /// <summary>
            /// MySQL
            /// </summary>
            MySQL,
        }

        /// <summary>
        /// 设置数据库类型
        /// </summary>
        /// <param name="dialect">数据库类型</param>
        /// <param name="isUpToLow">表名、属性名是否区分大小写（小写时候以下划线分词，类名：AaBb=>aa_bb）</param>
        public static void SetDialect(Dialect dialect, bool isUpToLow = true)
        {
            _isUpToLow = isUpToLow;
            switch (dialect)
            {
                case Dialect.PostgreSQL:
                    _dialect = Dialect.PostgreSQL;
                    _encapsulation = "\"{0}\"";
                    _getIdentitySql = string.Format("SELECT LASTVAL() AS id");
                    _getPagedListSql = "Select {SelectColumns} from {TableName} {WhereClause} Order By {OrderBy} LIMIT {RowsPerPage} OFFSET (({PageNumber}-1) * {RowsPerPage})";
                    break;

                case Dialect.SQLite:
                    _dialect = Dialect.SQLite;
                    _encapsulation = "\"{0}\"";
                    _getIdentitySql = string.Format("SELECT LAST_INSERT_ROWID() AS id");
                    _getPagedListSql = "Select {SelectColumns} from {TableName} {WhereClause} Order By {OrderBy} LIMIT {RowsPerPage} OFFSET (({PageNumber}-1) * {RowsPerPage})";
                    break;

                case Dialect.MySQL:
                    _dialect = Dialect.MySQL;
                    _encapsulation = "`{0}`";
                    _getIdentitySql = string.Format("SELECT LAST_INSERT_ID() AS id");
                    _getPagedListSql = "Select {SelectColumns} from {TableName} {WhereClause} Order By {OrderBy} LIMIT {Offset},{RowsPerPage}";
                    break;

                default:
                    _dialect = Dialect.SQLServer;
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
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>默认过滤器为Id字段/para>
        /// <para>-Id字段可以使用 [Key] 标签设定使用字段</para>
        /// <para>支持事物和命令超时设定</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="id">主键</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回T类型的单实例</returns>

        public static T Get<T>(this IDbConnection connection, object id, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return Get<T>(connection, null, id, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>默认过滤器为Id字段/para>
        /// <para>-Id字段可以使用 [Key] 标签设定使用字段</para>
        /// <para>支持事物和命令超时设定</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="id">主键</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回T类型的单实例</returns>
        public static T Get<T>(this IDbConnection connection, string tableName, object id, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var idProps = GetIdProperties(currenttype).ToList();

            if (!idProps.Any())
                throw new ArgumentException("Get<T> 仅支持实体类属性带有[Key]标签或属性名为Id");
            if (idProps.Count() > 1)
                throw new ArgumentException("Get<T> 仅支持唯一主键（属性带有[Key]或属性名为Id的）");

            var onlyKey = idProps.First();

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.Append("Select ");
            //创建一个空的基本类型属性的新实例
            BuildSelect(sb, GetScaffoldableProperties((T)Activator.CreateInstance(typeof(T))).ToArray());
            sb.AppendFormat(" from {0}", name);
            sb.Append(" where " + GetColumnName(onlyKey) + " = @Id");

            var dynParms = new DynamicParameters();
            dynParms.Add("@id", id);

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("Get<{0}>: {1} with Id: {2}", currenttype, sb, id));
            if (_isUpToLow)
            {
                var sdr = connection.ExecuteReader(sb.ToString(), dynParms, transaction, commandTimeout);
                return populate.GetSingle<T>(sdr);
            }
            return connection.Query<T>(sb.ToString(), dynParms, transaction, true, commandTimeout).FirstOrDefault();
        }

        /// <summary>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>whereConditions 使用方式: new {Category = 1, SubCategory=2} -非必须</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回符合whereConditions的IEnumerable<T>类型</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="whereConditions">过滤条件</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回符合whereConditions的IEnumerable<T>类型</returns>
        public static IEnumerable<T> GetList<T>(this IDbConnection connection, string tableName, object whereConditions, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var idProps = GetIdProperties(currenttype).ToList();
            if (!idProps.Any())
                throw new ArgumentException("实体类至少包含一个主键[Key]");

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            var whereprops = GetAllProperties(whereConditions).ToArray();
            sb.Append("Select ");
            //创建一个空的基本类型属性的新实例
            BuildSelect(sb, GetScaffoldableProperties((T)Activator.CreateInstance(typeof(T))).ToArray());
            sb.AppendFormat(" from {0}", name);

            if (whereprops.Any())
            {
                sb.Append(" where ");
                BuildWhere(sb, whereprops, (T)Activator.CreateInstance(typeof(T)), whereConditions);
            }

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("GetList<{0}>: {1}", currenttype, sb));
            if (_isUpToLow)
            {
                var sdr = connection.ExecuteReader(sb.ToString(), whereConditions, transaction, commandTimeout);
                return populate.GetList<T>(sdr);
            }
            return connection.Query<T>(sb.ToString(), whereConditions, transaction, true, commandTimeout);
        }

        /// <summary>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>conditions 使用方式: "where name='bob'" or "where age>=@Age" -非必须</para>
        /// <para>parameters 使用方式: new { Age = 15 } -非必须</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回符合conditions条件和parameters过滤的IEnumerable<T>类型</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自链接</param>
        /// <param name="tableName">表名</param>
        /// <param name="conditions">SqlWhere条件</param>
        /// <param name="parameters">参数化</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回符合conditions条件和parameters过滤的IEnumerable<T>类型</returns>
        public static IEnumerable<T> GetList<T>(this IDbConnection connection, string tableName, string conditions, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var idProps = GetIdProperties(currenttype).ToList();
            if (!idProps.Any())
                throw new ArgumentException("实体类至少包含一个主键[Key]");

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.Append("Select ");
            //创建一个空的基本类型属性的新实例
            BuildSelect(sb, GetScaffoldableProperties((T)Activator.CreateInstance(typeof(T))).ToArray());
            sb.AppendFormat(" from {0}", name);

            sb.Append(" " + conditions);

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("GetList<{0}>: {1}", currenttype, sb));
            if (_isUpToLow)
            {
                var sdr = connection.ExecuteReader(sb.ToString(), parameters, transaction, commandTimeout);
                return populate.GetList<T>(sdr);
            }
            return connection.Query<T>(sb.ToString(), parameters, transaction, true, commandTimeout);
        }

        /// <summary>
        /// <para>查询跟类名一致的表名</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>返回IEnumerable<T>类型</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自链接</param>
        /// <returns>返回IEnumerable<T>类型</returns>
        public static IEnumerable<T> GetList<T>(this IDbConnection connection)
        {
            return connection.GetList<T>(null, new { });
        }
        /// <summary>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>conditions 使用方式: "where name='bob'" or "where age>=@Age" -非必须</para>
        /// <para>orderby 使用方式: "lastname, age desc" -非必须 - 默认Key</para>
        /// <para>parameters 使用方式: new { Age = 15 } -非必须</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回符合conditions条件和parameters过滤的IEnumerable<T>类型</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自链接</param>
        /// <param name="pageNumber">页码</param>
        /// <param name="rowsPerPage">每页条数</param>
        /// <param name="conditions">SqlWhere条件</param>
        /// <param name="orderby">排序字段</param>
        /// <param name="parameters">参数化</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回符合conditions条件和parameters过滤的IEnumerable<T>类型</returns>
        public static IEnumerable<T> GetListPaged<T>(this IDbConnection connection, string tableName, int pageNumber, int rowsPerPage, string conditions, string orderby, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            if (string.IsNullOrEmpty(_getPagedListSql))
                throw new Exception("GetListPage 不支持当前sql语言");

            if (pageNumber < 1)
                throw new Exception("页码从1开始");

            var currenttype = typeof(T);
            var idProps = GetIdProperties(currenttype).ToList();
            if (!idProps.Any())
                throw new ArgumentException("实体类至少包含一个主键[Key]");

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            var query = _getPagedListSql;
            if (string.IsNullOrEmpty(orderby))
            {
                orderby = GetColumnName(idProps.First());
            }
            //创建一个空的基本类型属性的新实例
            BuildSelect(sb, GetScaffoldableProperties((T)Activator.CreateInstance(typeof(T))).ToArray());
            query = query.Replace("{SelectColumns}", sb.ToString());
            query = query.Replace("{TableName}", name);
            query = query.Replace("{PageNumber}", pageNumber.ToString());
            query = query.Replace("{RowsPerPage}", rowsPerPage.ToString());
            query = query.Replace("{OrderBy}", orderby);
            query = query.Replace("{WhereClause}", conditions);
            query = query.Replace("{Offset}", ((pageNumber - 1) * rowsPerPage).ToString());

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("GetListPaged<{0}>: {1}", currenttype, query));
            if (_isUpToLow)
            {
                var sdr = connection.ExecuteReader(query.ToString(), parameters, transaction, commandTimeout);
                return populate.GetList<T>(sdr);
            }
            return connection.Query<T>(query, parameters, transaction, true, commandTimeout);
        }

        /// <summary>
        /// <para>查询跟类名一致的表名</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>conditions 使用方式: "where name='bob'" or "where age>=@Age" -非必须</para>
        /// <para>orderby 使用方式: "lastname, age desc" -非必须 - 默认Key</para>
        /// <para>parameters 使用方式: new { Age = 15 } -非必须</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回符合conditions条件和parameters过滤的IEnumerable<T>类型</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自链接</param>
        /// <param name="pageNumber">页码</param>
        /// <param name="rowsPerPage">每页条数</param>
        /// <param name="conditions">SqlWhere条件</param>
        /// <param name="orderby">排序字段</param>
        /// <param name="parameters">参数化</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回符合conditions条件和parameters过滤的IEnumerable<T>类型</returns>
        public static IEnumerable<T> GetListPaged<T>(this IDbConnection connection, int pageNumber, int rowsPerPage, string conditions, string orderby, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.GetListPaged<T>(null, pageNumber, rowsPerPage, conditions, orderby, parameters, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>插入一条数据到数据库（支持简单类型）</para>
        /// <para>默认类名一致的表名</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>插入的主键属性为Id或者带有[Key]标签的属性名</para>
        /// <para>带有 [Editable(false)]标签或者复杂的类型将会被忽略</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回主键Id或者自动生成的主键值</para>
        /// </summary>
        /// <param name="connection">自连接</param>
        /// <param name="entityToInsert">插入的实体对象</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回主键Id或者自动生成的主键值</returns>
        public static int? Insert(this IDbConnection connection, object entityToInsert, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return Insert<int?>(connection, null, entityToInsert, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>插入一条数据到数据库（支持简单类型）</para>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>插入的主键属性为Id或者带有[Key]标签的属性名</para>
        /// <para>带有 [Editable(false)]标签或者复杂的类型将会被忽略</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回主键Id或者自动生成的主键值</para>
        /// </summary>
        /// <typeparam name="TKey">主键类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="entityToInsert">插入的实体对象</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回主键Id或者自动生成的主键值</returns>
        public static TKey Insert<TKey>(this IDbConnection connection, string tableName, object entityToInsert, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var idProps = GetIdProperties(entityToInsert).ToList();

            if (!idProps.Any())
                throw new ArgumentException("Insert<T>仅支持实体类属性带有[Key]标签或属性名为Id");
            if (idProps.Count() > 1)
                throw new ArgumentException("Insert<T>仅支持唯一主键（属性带有[Key]或属性名为Id的");

            var keyHasPredefinedValue = false;
            var baseType = typeof(TKey);
            var underlyingType = Nullable.GetUnderlyingType(baseType);
            var keytype = underlyingType ?? baseType;
            if (keytype != typeof(int) && keytype != typeof(uint) && keytype != typeof(long) && keytype != typeof(ulong) && keytype != typeof(short) && keytype != typeof(ushort) && keytype != typeof(Guid) && keytype != typeof(string))
            {
                throw new Exception("Invalid return type");
            }

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(entityToInsert);

            var sb = new StringBuilder();
            sb.AppendFormat("insert into {0}", name);
            sb.Append(" (");
            BuildInsertParameters(entityToInsert, sb);
            sb.Append(") ");
            sb.Append("values");
            sb.Append(" (");
            BuildInsertValues(entityToInsert, sb);
            sb.Append(")");

            if (keytype == typeof(Guid))
            {
                var guidvalue = (Guid)idProps.First().GetValue(entityToInsert, null);
                if (guidvalue == Guid.Empty)
                {
                    var newguid = SequentialGuid();
                    idProps.First().SetValue(entityToInsert, newguid, null);
                }
                else
                {
                    keyHasPredefinedValue = true;
                }
                sb.Append(";select '" + idProps.First().GetValue(entityToInsert, null) + "' as id");
            }

            if ((keytype == typeof(int) || keytype == typeof(long)) && Convert.ToInt64(idProps.First().GetValue(entityToInsert, null)) == 0)
            {
                sb.Append(";" + _getIdentitySql);
            }
            else
            {
                keyHasPredefinedValue = true;
            }

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("Insert: {0}", sb));

            var r = connection.Query(sb.ToString(), entityToInsert, transaction, true, commandTimeout);

            if (keytype == typeof(Guid) || keyHasPredefinedValue)
            {
                return (TKey)idProps.First().GetValue(entityToInsert, null);
            }
            return (TKey)r.First().id;
        }

        /// <summary>
        /// <para>插入一条数据到数据库（包含主键全插入生成）</para>
        /// <para>默认类名对应的表名</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="entity">插入的数据对象</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns></returns>
        public static bool Insert<T>(this IDbConnection connection, T entity, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return Insert<T>(connection, null, entity, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>插入一条数据到数据库（包含主键全插入生成）</para>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="entity">插入的数据对象</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns></returns>
        public static bool Insert<T>(this IDbConnection connection, string tableName, T entity, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(entity);
            var sb = new StringBuilder();
            sb.AppendFormat("insert into {0}", name);
            sb.Append(" (");
            var props = entity.GetType().GetProperties();
            for (var i = 0; i < props.Count(); i++)
            {
                var property = props.ElementAt(i);

                sb.Append(GetColumnName(property));
                if (i < props.Count() - 1)
                    sb.Append(", ");
            }
            if (sb.ToString().EndsWith(", "))
                sb.Remove(sb.Length - 2, 2);
            sb.Append(") ");
            sb.Append("values");
            sb.Append(" (");
            for (var i = 0; i < props.Count(); i++)
            {
                var property = props.ElementAt(i);
                sb.AppendFormat("@{0}", property.Name);
                if (i < props.Count() - 1)
                    sb.Append(", ");
            }
            if (sb.ToString().EndsWith(", "))
                sb.Remove(sb.Length - 2, 2);
            sb.Append(")");

            connection.Query(sb.ToString(), entity, transaction, true, commandTimeout);
            return true;
        }

        /// <summary>
        /// <para>更新一条或多条数据到数据库</para>
        /// <para>默认类名一致的表名</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>更新Id或者带有[Key]标签的属性的值一致的对象</para>
        /// <para>带有 [Editable(false)]标签或者复杂的类型将会被忽略</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回影响的行数</para>
        /// </summary>
        /// <param name="connection">自连接</param>
        /// <param name="entityToUpdate">更新对象</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int Update(this IDbConnection connection, object entityToUpdate, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return Update(connection, null, entityToUpdate, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>更新一条或多条数据到数据库</para>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>更新Id或者带有[Key]标签的属性的值一致的对象</para>
        /// <para>带有 [Editable(false)]标签或者复杂的类型将会被忽略</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回影响的行数</para>
        /// </summary>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="entityToUpdate">更新对象</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int Update(this IDbConnection connection, string tableName, object entityToUpdate, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var idProps = GetIdProperties(entityToUpdate).ToList();

            if (!idProps.Any())//请注意Any和count的使用场景
                throw new ArgumentException("Entity must have at least one [Key] or Id property");

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(entityToUpdate);

            var sb = new StringBuilder();
            sb.AppendFormat("update {0}", name);

            sb.AppendFormat(" set ");
            BuildUpdateSet(entityToUpdate, sb);
            sb.Append(" where ");
            BuildWhere(sb, idProps, entityToUpdate);

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("Update: {0}", sb));

            return connection.Execute(sb.ToString(), entityToUpdate, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>删除一条或者多条数据符合传入的对象</para>
        /// <para>-自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回影响的行数</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="entityToDelete">实体对象</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int Delete<T>(this IDbConnection connection, string tableName, T entityToDelete, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var idProps = GetIdProperties(entityToDelete).ToList();

            if (!idProps.Any())
                throw new ArgumentException("实体必须包含带有[Key]标签或者名称为Id的属性名");

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(entityToDelete);

            var sb = new StringBuilder();
            sb.AppendFormat("delete from {0}", name);

            sb.Append(" where ");
            BuildWhere(sb, idProps, entityToDelete);

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("Delete: {0}", sb));

            return connection.Execute(sb.ToString(), entityToDelete, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>根据Id删除相应的数据（联合主键取第一个）</para>
        /// <para>-自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>支持事物和命令超时设定</para>
        /// <para>返回影响的行数</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="id">主键</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int Delete<T>(this IDbConnection connection, string tableName, object id, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var idProps = GetIdProperties(currenttype).ToList();

            if (!idProps.Any())
                throw new ArgumentException("Delete<T>仅支持实体类属性带有[Key]标签或属性名为Id");
            if (idProps.Count > 1)
                throw new ArgumentException("Delete<T>仅支持唯一主键（属性带有[Key]或属性名为Id的");

            var onlyKey = idProps.First();
            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.AppendFormat("Delete from {0}", name);
            sb.Append(" where ").Append(GetColumnName(onlyKey)).Append(" = @Id");

            var dynParms = new DynamicParameters();
            dynParms.Add("@id", id);

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("Delete<{0}> {1}", currenttype, sb));

            return connection.Execute(sb.ToString(), dynParms, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>删除符合whereConditions的一系列数据</para>
        /// <para>-默认类名一致的表名</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>whereConditions 使用方式: new {Category = 1, SubCategory=2} -非必须</para>
        /// <para>返回影响的行数</para>
        /// <para>支持事物和命令超时设定</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="whereConditions"></param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int DeleteList<T>(this IDbConnection connection, object whereConditions, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return DeleteList<T>(connection, null, whereConditions, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>删除符合whereConditions的一系列数据</para>
        /// <para>-自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>whereConditions 使用方式: new {Category = 1, SubCategory=2} -非必须</para>
        /// <para>返回影响的行数</para>
        /// <para>支持事物和命令超时设定</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="whereConditions"></param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int DeleteList<T>(this IDbConnection connection, string tableName, object whereConditions, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            var whereprops = GetAllProperties(whereConditions).ToArray();
            sb.AppendFormat("Delete from {0}", name);
            if (whereprops.Any())
            {
                sb.Append(" where ");
                BuildWhere(sb, whereprops, (T)Activator.CreateInstance(typeof(T)));
            }

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("DeleteList<{0}> {1}", currenttype, sb));

            return connection.Execute(sb.ToString(), whereConditions, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>删除符合过滤条件的一系列数据</para>
        /// <para>-自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>conditions 使用方式: "where name='bob'" or "where age>=@Age" -非必须</para>
        /// <para>parameters 使用方式: new { Age = 15 } -非必须</para>
        /// <para>返回影响的行数</para>
        /// <para>支持事物和命令超时设定</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="conditions">SqlWhere条件</param>
        /// <param name="parameters">参数化</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int DeleteList<T>(this IDbConnection connection, string tableName, string conditions, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            if (string.IsNullOrEmpty(conditions))
                throw new ArgumentException("DeleteList<T> 限定条件必填");
            if (!conditions.ToLower().Contains("where"))
                throw new ArgumentException("DeleteList<T> 限定条件必填且必须带有where开头关键字");

            var currenttype = typeof(T);
            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.AppendFormat("Delete from {0}", name);
            sb.Append(" " + conditions);

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("DeleteList<{0}> {1}", currenttype, sb));

            return connection.Execute(sb.ToString(), parameters, transaction, commandTimeout);
        }
        /// <summary>
        /// <para>默认统计数据条数</para>
        /// <para>-自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>返回影响的行数</para>
        /// <para>支持事物和命令超时设定</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int RecordCount<T>(this IDbConnection connection, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.RecordCount<T>(null,null,transaction,commandTimeout);
        }
        /// <summary>
        /// <para>根据过滤条件统计数据条数</para>
        /// <para>-自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>conditions 使用方式: "where name='bob'" or "where age>=@Age" -非必须</para>
        /// <para>parameters 使用方式: new { Age = 15 } -非必须</para>
        /// <para>返回影响的行数</para>
        /// <para>支持事物和命令超时设定</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="conditions">SqlWhere条件</param>
        /// <param name="parameters">参数化</param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int RecordCount<T>(this IDbConnection connection, string tableName, string conditions, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.Append("Select count(1)");
            sb.AppendFormat(" from {0}", name);
            sb.Append(" " + conditions);

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("RecordCount<{0}>: {1}", currenttype, sb));

            return connection.ExecuteScalar<int>(sb.ToString(), parameters, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>根据过滤条件统计数据条数</para>
        /// <para>-自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>whereConditions 使用方式: new {Category = 1, SubCategory=2} -非必须</para>
        /// <para>返回影响的行数</para>
        /// <para>支持事物和命令超时设定</para>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="connection">自连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="whereConditions"></param>
        /// <param name="transaction">事物</param>
        /// <param name="commandTimeout">超时</param>
        /// <returns>返回影响的行数</returns>
        public static int RecordCount<T>(this IDbConnection connection, string tableName, object whereConditions, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            var whereprops = GetAllProperties(whereConditions).ToArray();
            sb.Append("Select count(1)");
            sb.AppendFormat(" from {0}", name);
            if (whereprops.Any())
            {
                sb.Append(" where ");
                BuildWhere(sb, whereprops, (T)Activator.CreateInstance(typeof(T)));
            }

            if (Debugger.IsAttached)
                Console.WriteLine(String.Format("RecordCount<{0}>: {1}", currenttype, sb));

            return connection.ExecuteScalar<int>(sb.ToString(), whereConditions, transaction, commandTimeout);
        }


        /// <summary>
        /// 创建update参数可变字符串（a=1）
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

        //build select clause based on list of properties skipping ones with the IgnoreSelect and NotMapped attribute
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
                //if there is a custom column name add an "as customcolumnname" to the item so it maps properly
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

                //match up generic properties to source entity properties to allow fetching of the column attribute
                //the anonymous object used for search doesn't have the custom attributes attached to them so this allows us to build the correct where clause
                //by converting the model type to the database column name via the column attribute
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

                if (i < propertyInfos.Count() - 1)
                    sb.AppendFormat(" and ");
            }
        }

        //build insert values which include all properties in the class that are:
        //Not named Id
        //Not marked with the Editable(false) attribute
        //Not marked with the [Key] attribute (without required attribute)
        //Not marked with [IgnoreInsert]
        //Not marked with [NotMapped]
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
                if (i < props.Count() - 1)
                    sb.Append(", ");
            }
            if (sb.ToString().EndsWith(", "))
                sb.Remove(sb.Length - 2, 2);
        }

        //build insert parameters which include all properties in the class that are not:
        //marked with the Editable(false) attribute
        //marked with the [Key] attribute
        //marked with [IgnoreInsert]
        //named Id
        //marked with [NotMapped]
        private static void BuildInsertParameters(object entityToInsert, StringBuilder sb)
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

                sb.Append(GetColumnName(property));
                if (i < props.Count() - 1)
                    sb.Append(", ");
            }
            if (sb.ToString().EndsWith(", "))
                sb.Remove(sb.Length - 2, 2);
        }

        //Get all properties in an entity
        private static IEnumerable<PropertyInfo> GetAllProperties(object entity)
        {
            if (entity == null) entity = new { };
            return entity.GetType().GetProperties();
        }

        //Get all properties that are not decorated with the Editable(false) attribute
        private static IEnumerable<PropertyInfo> GetScaffoldableProperties(object entity)
        {
            var props = entity.GetType().GetProperties().Where(p => p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(EditableAttribute).Name && !IsEditable(p)) == false);
            return props.Where(p => p.PropertyType.IsSimpleType() || IsEditable(p));
        }

        //Determine if the Attribute has an AllowEdit key and return its boolean state
        //fake the funk and try to mimick EditableAttribute in System.ComponentModel.DataAnnotations
        //This allows use of the DataAnnotations property in the model and have the SimpleCRUD engine just figure it out without a reference
        private static bool IsEditable(PropertyInfo pi)
        {
            var attributes = pi.GetCustomAttributes(false);
            if (attributes.Count() > 0)
            {
                dynamic write = attributes.FirstOrDefault(x => x.GetType().Name == typeof(EditableAttribute).Name);
                if (write != null)
                {
                    return write.AllowEdit;
                }
            }
            return false;
        }

        //Determine if the Attribute has an IsReadOnly key and return its boolean state
        //fake the funk and try to mimick ReadOnlyAttribute in System.ComponentModel
        //This allows use of the DataAnnotations property in the model and have the SimpleCRUD engine just figure it out without a reference
        private static bool IsReadOnly(PropertyInfo pi)
        {
            var attributes = pi.GetCustomAttributes(false);
            if (attributes.Count() > 0)
            {
                dynamic write = attributes.FirstOrDefault(x => x.GetType().Name == typeof(ReadOnlyAttribute).Name);
                if (write != null)
                {
                    return write.IsReadOnly;
                }
            }
            return false;
        }

        //Get all properties that are:
        //Not named Id
        //Not marked with the Key attribute
        //Not marked ReadOnly
        //Not marked IgnoreInsert
        //Not marked NotMapped
        private static IEnumerable<PropertyInfo> GetUpdateableProperties(object entity)
        {
            var updateableProperties = GetScaffoldableProperties(entity);
            //remove ones with ID
            updateableProperties = updateableProperties.Where(p => !p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
            //remove ones with key attribute
            updateableProperties = updateableProperties.Where(p => p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(KeyAttribute).Name) == false);
            //remove ones that are readonly
            updateableProperties = updateableProperties.Where(p => p.GetCustomAttributes(true).Any(attr => (attr.GetType().Name == typeof(ReadOnlyAttribute).Name) && IsReadOnly(p)) == false);
            //remove ones with IgnoreUpdate attribute
            updateableProperties = updateableProperties.Where(p => p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(IgnoreUpdateAttribute).Name) == false);
            //remove ones that are not mapped
            updateableProperties = updateableProperties.Where(p => p.GetCustomAttributes(true).Any(attr => attr.GetType().Name == typeof(NotMappedAttribute).Name) == false);

            return updateableProperties;
        }

        //Get all properties that are named Id or have the Key attribute
        //For Inserts and updates we have a whole entity so this method is used
        private static IEnumerable<PropertyInfo> GetIdProperties(object entity)
        {
            var type = entity.GetType();
            return GetIdProperties(type);
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
            string tableName;

            if (TableNames.TryGetValue(type, out tableName))
                return tableName;

            tableName = _tableNameResolver.ResolveTableName(type);
            if (_isUpToLow)
                tableName = GetFieldNameByUpperToLower(tableName);
            TableNames[type] = tableName;

            return tableName;
        }

        ///
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
                        sb.Append("_" + char.ToLower(c));
                    }
                    else
                    {
                        sb.Append(char.ToLower(c));
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 对应数据库封装后的属性名或者表名
        /// </summary>
        /// <param name="databaseword"></param>
        /// <returns></returns>
        private static string Encapsulate(string databaseword)
        {
            return string.Format(_encapsulation, databaseword);
        }

        /// <summary>
        /// 生成一个以当前时间位基础的GUDI
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
                        Console.WriteLine(String.Format("属性名由 {0} 重写到 {1}", propertyInfo.Name, columnName));
                }
                return columnName;
            }
        }
    } 
}