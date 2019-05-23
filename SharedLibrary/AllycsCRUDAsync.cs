namespace Dapper
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

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
        public static async Task<T> GetAsync<T>(this IDbConnection connection, object id, string tableName = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var currenttype = typeof(T);
            var idProps = GetIdProperties(currenttype).ToList();

            if (idProps.Count == 0)
                throw new ArgumentException("Get<T> 仅支持实体类属性带有[Key]标签或属性名为Id");
            if (idProps.Count > 1)
                throw new ArgumentException("Get<T> 仅支持唯一主键（属性带有[Key]或属性名为Id的");

            var name = tableName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetTableName(currenttype);

            var sb = new StringBuilder();
            sb.Append("SELECT ");
            //创建一个空的基本类型属性的新实例
            BuildSelect(sb, GetScaffoldableProperties<T>().ToArray());
            sb.AppendFormat(" FROM {0} WHERE ", name);

            for (var i = 0; i < idProps.Count; i++)
            {
                if (i > 0)
                    sb.Append(" and ");
                sb.AppendFormat("{0} = @{1}", GetColumnName(idProps[i]), idProps[i].Name);
            }

            var dynParms = new DynamicParameters();
            if (idProps.Count == 1)
                dynParms.Add("@" + idProps.First().Name, id);
            else
            {
                foreach (var prop in idProps)
                    dynParms.Add("@" + prop.Name, id.GetType().GetProperty(prop.Name).GetValue(id, null));
            }

            if (Debugger.IsAttached)
                Debug.WriteLine(String.Format("Get<{0}>: {1} with Id: {2}", currenttype, sb, id));
            T result;
            if (_isUpToLow)
            {
                var sdr = await connection.ExecuteReaderAsync(sb.ToString(), dynParms, transaction, commandTimeout).ConfigureAwait(false);
                result = populate.GetSingle<T>(sdr);
            }
            else
            {
                var query = await connection.QueryAsync<T>(sb.ToString(), dynParms, transaction, commandTimeout).ConfigureAwait(false);
                result = query.FirstOrDefault();
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
        public static async Task<IEnumerable<T>> GetListAsync<T>(this IDbConnection connection, object whereConditions, string tableName = null, IDbTransaction transaction = null, int? commandTimeout = null)
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
                var sdr = await connection.ExecuteReaderAsync(sb.ToString(), whereConditions, transaction, commandTimeout).ConfigureAwait(false);
                result = populate.GetList<T>(sdr);
            }
            else
            {
                result = await connection.QueryAsync<T>(sb.ToString(), whereConditions, transaction, commandTimeout).ConfigureAwait(false);
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
        public static async Task<IEnumerable<T>> GetListAsync<T>(this IDbConnection connection, string conditions, string tableName = null, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
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
                var sdr = await connection.ExecuteReaderAsync(sb.ToString(), parameters, transaction, commandTimeout).ConfigureAwait(false);
                result = populate.GetList<T>(sdr);
            }
            else
            {
                result = await connection.QueryAsync<T>(sb.ToString(), parameters, transaction, commandTimeout).ConfigureAwait(false);
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
        public static async Task<IEnumerable<T>> GetListPagedAsync<T>(this IDbConnection connection, int pageNumber, int rowsPerPage, string conditions, string orderby, string tableName = null, object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
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
            IEnumerable<T> result;
            if (_isUpToLow)
            {
                using (var sdr = await connection.ExecuteReaderAsync(query, parameters, transaction, commandTimeout).ConfigureAwait(false))
                {
                    result = populate.GetList<T>(sdr);
                }
            }
            else
            {
                result = await connection.QueryAsync<T>(query, parameters, transaction, commandTimeout).ConfigureAwait(false);
            }
            connection.ConnClose();
            return result;
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
        public static async Task<TKey> InsertAsync<TKey>(this IDbConnection connection, object entityToInsert, string tableName = null, IDbTransaction transaction = null, int? commandTimeout = null)
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
                sb.Append(";select '").Append(idProps.First().GetValue(entityToInsert, null)).Append("' as id");
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
                Debug.WriteLine(String.Format("Insert: {0}", sb));

            var r = await connection.QueryAsync(sb.ToString(), entityToInsert, transaction, commandTimeout).ConfigureAwait(false);

            if (keytype == typeof(Guid) || keyHasPredefinedValue)
            {
                return (TKey)idProps[0].GetValue(entityToInsert, null);
            }
            return (TKey)r.First().id;
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
        public static async Task<bool> InsertAsync<T>(this IDbConnection connection, string tableName, T entity, IDbTransaction transaction = null, int? commandTimeout = null)
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
            sb.Append("values");
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

            _ = await connection.QueryAsync(sb.ToString(), entity, transaction, commandTimeout).ConfigureAwait(false);
            return true;
        }

    }
}