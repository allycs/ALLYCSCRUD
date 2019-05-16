namespace Demo_DotNetCore
{
    using System.Configuration;
    using System.Data;
    using System.Data.SqlClient;

    public class DbHelper
    {
        private static readonly string connStr = "Server=.;Database=demo;Uid=sa;Pwd=123456";

        public static IDbConnection CreateConnection()
        {
            return new SqlConnection(connStr);
        }
    }
}
