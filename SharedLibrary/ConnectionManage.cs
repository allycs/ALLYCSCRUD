namespace Dapper
{
    using System.Data;

    /// <summary>
    /// 数据库连接管理类
    /// </summary>
    public static class ConnectionManage
    {
        /// <summary>
        /// 关闭当前连接
        /// </summary>
        /// <param name="connection"></param>
        public static void ConnClose(this IDbConnection connection)
        {
            if (connection != null && connection.State != ConnectionState.Closed)
            {
                connection.Close();
                connection.Dispose();
            }
        }
    }
}