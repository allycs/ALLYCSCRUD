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
                var sdr = await connection.ExecuteReaderAsync(sb.ToString(), dynParms, transaction, commandTimeout);
                result = populate.GetSingle<T>(sdr);
            }
            else
            {
                var query = await connection.QueryAsync<T>(sb.ToString(), dynParms, transaction, commandTimeout);
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
                var sdr = await connection.ExecuteReaderAsync(sb.ToString(), whereConditions, transaction, commandTimeout);
                result = populate.GetList<T>(sdr);
            }
            else
            {
                result = await connection.QueryAsync<T>(sb.ToString(), whereConditions, transaction, commandTimeout);
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
                var sdr = await connection.ExecuteReaderAsync(sb.ToString(), parameters, transaction, commandTimeout);
                result = populate.GetList<T>(sdr);
            }
            else
            {
                result = await connection.QueryAsync<T>(sb.ToString(), parameters, transaction, commandTimeout);
            }
            connection.ConnClose();
            return result;
        }
    }
}