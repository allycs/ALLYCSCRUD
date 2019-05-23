namespace Dapper
{
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
        /// <summary>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>默认过滤器为Id字段</para>
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
        public static T Get<T>(this IDbConnection connection, object id, string tableName = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var idProps = GetIdProperties(currenttype).ToList();

            if (idProps.Count == 0)
                throw new ArgumentException("Get<T> 仅支持实体类属性带有[Key]标签或属性名为Id");
            if (idProps.Count > 1)
                throw new ArgumentException("Get<T> 仅支持唯一主键（属性带有[Key]或属性名为Id的");

            var onlyKey = idProps[0];

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.Append("SELECT ");
            //创建一个空的基本类型属性的新实例
            BuildSelect(sb, GetScaffoldableProperties((T)Activator.CreateInstance(typeof(T))).ToArray());
            sb.AppendFormat(" FROM {0}", name);
            sb.Append(" WHERE ").Append(GetColumnName(onlyKey)).Append(" = @Id");

            var dynParms = new DynamicParameters();
            dynParms.Add("@id", id);

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("Get<{0}>: {1} with Id: {2}", currenttype, sb, id));
            T result;
            if (_isUpToLow)
            {
                var sdr = connection.ExecuteReader(sb.ToString(), dynParms, transaction, commandTimeout);
                result = populate.GetSingle<T>(sdr);
            }
            else
            {
                result = connection.Query<T>(sb.ToString(), dynParms, transaction, true, commandTimeout).FirstOrDefault();
            }

            connection.ConnClose();
            return result;
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
        public static IEnumerable<T> GetList<T>(this IDbConnection connection, object whereConditions, string tableName = null, IDbTransaction transaction = null, int? commandTimeout = null)
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
            sb.Append("SELECT ");
            //创建一个空的基本类型属性的新实例
            BuildSelect(sb, GetScaffoldableProperties((T)Activator.CreateInstance(typeof(T))).ToArray());
            sb.AppendFormat(" FROM {0}", name);

            if (whereprops.Any())
            {
                sb.Append(" WHERE ");
                BuildWhere(sb, whereprops, (T)Activator.CreateInstance(typeof(T)), whereConditions);
            }

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("GetList<{0}>: {1}", currenttype, sb));
            IEnumerable<T> result;
            if (_isUpToLow)
            {
                var sdr = connection.ExecuteReader(sb.ToString(), whereConditions, transaction, commandTimeout);
                result = populate.GetList<T>(sdr);
            }
            else
            {
                result = connection.Query<T>(sb.ToString(), whereConditions, transaction, true, commandTimeout);
            }
            connection.ConnClose();
            return result;
        }

        /// <summary>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>conditions 使用方式: "WHERE name='bob'" or "WHERE age>=@Age" -非必须</para>
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
        public static IEnumerable<T> GetList<T>(this IDbConnection connection, string conditions, string tableName = null, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var idProps = GetIdProperties(currenttype).ToList();
            if (!idProps.Any())
                throw new ArgumentException("实体类至少包含一个主键[Key]");

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.Append("SELECT ");
            //创建一个空的基本类型属性的新实例
            BuildSelect(sb, GetScaffoldableProperties((T)Activator.CreateInstance(typeof(T))).ToArray());
            sb.AppendFormat(" FROM {0} ", name);
            sb.Append(conditions);

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("GetList<{0}>: {1}", currenttype, sb));
            IEnumerable<T> result;
            if (_isUpToLow)
            {
                var sdr = connection.ExecuteReader(sb.ToString(), parameters, transaction, commandTimeout);
                result = populate.GetList<T>(sdr);
            }
            else
            {
                result = connection.Query<T>(sb.ToString(), parameters, transaction, true, commandTimeout);
            }
            connection.ConnClose();
            return result;
        }

        /// <summary>
        /// <para>自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>conditions 使用方式: "WHERE name='bob'" or "WHERE age>=@Age" -非必须</para>
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
        public static IEnumerable<T> GetListPaged<T>(this IDbConnection connection, int pageNumber, int rowsPerPage, string conditions, string orderby, string tableName = null, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
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
                Debug.WriteLine(String.Format("GetListPaged<{0}>: {1}", currenttype, query));
            if (_isUpToLow)
            {
                var sdr = connection.ExecuteReader(query, parameters, transaction, commandTimeout);
                return populate.GetList<T>(sdr);
            }
            return connection.Query<T>(query, parameters, transaction, true, commandTimeout);
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
            return Insert<int?>(connection, entityToInsert, null, transaction, commandTimeout);
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
        public static TKey Insert<TKey>(this IDbConnection connection, object entityToInsert, string tableName = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var idProps = GetIdProperties(entityToInsert).ToList();

            if (!idProps.Any())
                throw new ArgumentException("Insert<T> 仅支持实体类属性带有[Key]标签或属性名为Id");
            if (idProps.Count > 1)
                throw new ArgumentException("Insert<T> 仅支持唯一主键（属性带有[Key]或属性名为Id的");

            var keyHasPredefinedValue = false;
            var baseType = typeof(TKey);
            var underlyingType = Nullable.GetUnderlyingType(baseType);
            var keytype = underlyingType ?? baseType;
            if (keytype != typeof(int) && keytype != typeof(uint) && keytype != typeof(long) && keytype != typeof(ulong) && keytype != typeof(short) && keytype != typeof(ushort) && keytype != typeof(Guid) && keytype != typeof(string))
            {
                throw new Exception("无效的返回类型");
            }

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(entityToInsert);

            var sb = new StringBuilder();
            sb.AppendFormat("INSERT into {0}", name);
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
                    idProps[0].SetValue(entityToInsert, newguid, null);
                }
                else
                {
                    keyHasPredefinedValue = true;
                }
                sb.Append(";SELECT '").Append(idProps.First().GetValue(entityToInsert, null)).Append("' AS id");
            }

            if ((keytype == typeof(int) || keytype == typeof(long)) && Convert.ToInt64(idProps.First().GetValue(entityToInsert, null)) == 0)
            {
                sb.Append(";").Append(_getIdentitySql);
            }
            else
            {
                keyHasPredefinedValue = true;
            }

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("INSERT: {0}", sb));

            var r = connection.Query(sb.ToString(), entityToInsert, transaction, true, commandTimeout);

            if (keytype == typeof(Guid) || keyHasPredefinedValue)
            {
                return (TKey)idProps[0].GetValue(entityToInsert, null);
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
            return Insert(connection, null, entity, transaction, commandTimeout);
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
            sb.AppendFormat("INSERT into {0}", name);
            sb.Append(" (");
            var props = entity.GetType().GetProperties();
            for (var i = 0; i < props.Length; i++)
            {
                var property = props.ElementAt(i);

                sb.Append(GetColumnName(property));
                if (i < props.Length - 1)
                    sb.Append(", ");
            }
            if (sb.ToString().EndsWith(", "))
                sb.Remove(sb.Length - 2, 2);
            sb.Append(") ");
            sb.Append("VALUES");
            sb.Append(" (");
            for (var i = 0; i < props.Length; i++)
            {
                var property = props.ElementAt(i);
                sb.AppendFormat("@{0}", property.Name);
                if (i < props.Length - 1)
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

            if (!idProps.Any())
                throw new ArgumentException("实体对象至少含有一个主键（以Id命名或者带有[Key]标签）");

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(entityToUpdate);

            var sb = new StringBuilder();
            sb.AppendFormat("UPDATE {0}", name);

            sb.AppendFormat(" SET ");
            BuildUpdateSet(entityToUpdate, sb);
            sb.Append(" WHERE ");
            BuildWhere(sb, idProps, entityToUpdate);

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("UPDATE: {0}", sb));
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
            sb.AppendFormat("delete FROM {0}", name);

            sb.Append(" WHERE ");
            BuildWhere(sb, idProps, entityToDelete);

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("Delete: {0}", sb));

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
                throw new ArgumentException("Delete<T> 仅支持实体类属性带有[Key]标签或属性名为Id");
            if (idProps.Count > 1)
                throw new ArgumentException("Delete<T> 仅支持唯一主键（属性带有[Key]或属性名为Id的");

            var onlyKey = idProps[0];
            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.AppendFormat("Delete FROM {0}", name);
            sb.Append(" WHERE ").Append(GetColumnName(onlyKey)).Append(" = @Id");

            var dynParms = new DynamicParameters();
            dynParms.Add("@id", id);

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("Delete<{0}> {1}", currenttype, sb));

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
            sb.AppendFormat("Delete FROM {0}", name);
            if (whereprops.Any())
            {
                sb.Append(" WHERE ");
                BuildWhere(sb, whereprops, (T)Activator.CreateInstance(typeof(T)));
            }

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("DeleteList<{0}> {1}", currenttype, sb));

            return connection.Execute(sb.ToString(), whereConditions, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>删除符合过滤条件的一系列数据</para>
        /// <para>-自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>conditions 使用方式: "WHERE name='bob'" or "WHERE age>=@Age" -非必须</para>
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
                throw new ArgumentException("DeleteList<T> requires a WHERE clause");
            if (!conditions.ToLower().Contains("where"))
                throw new ArgumentException("DeleteList<T> requires a WHERE clause and must contain the WHERE keyword");

            var currenttype = typeof(T);
            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.AppendFormat("Delete FROM {0}", name);
            sb.Append(" " + conditions);

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("DeleteList<{0}> {1}", currenttype, sb));

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
            return connection.RecordCount<T>(null, null, transaction, commandTimeout);
        }

        /// <summary>
        /// <para>根据过滤条件统计数据条数</para>
        /// <para>-自定义表名为空或者null取默认表名称</para>
        /// <para>-表名可以用在类名上加入 [Table("你的表名")]标签的方式重写</para>
        /// <para>conditions 使用方式: "WHERE name='bob'" or "WHERE age>=@Age" -非必须</para>
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
        public static int RecordCount<T>(this IDbConnection connection, string conditions, string tableName = null, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.Append("SELECT COUNT(1)");
            sb.AppendFormat(" FROM {0} ", name);
            sb.Append(conditions);

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("RecordCount<{0}>: {1}", currenttype, sb));

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
        public static int RecordCount<T>(this IDbConnection connection, object whereConditions, string tableName = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            var whereprops = GetAllProperties(whereConditions).ToArray();
            sb.Append("SELECT COUNT(1)");
            sb.AppendFormat(" FROM {0}", name);
            if (whereprops.Any())
            {
                sb.Append(" WHERE ");
                BuildWhere(sb, whereprops, (T)Activator.CreateInstance(typeof(T)));
            }

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("RecordCount<{0}>: {1}", currenttype, sb));

            return connection.ExecuteScalar<int>(sb.ToString(), whereConditions, transaction, commandTimeout);
        }
    }
}