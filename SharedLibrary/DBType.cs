namespace Dapper
{
    /// <summary>
    /// 支持数据库类型
    /// </summary>
    public enum DBType : int
    {
        SQLServer = 0 * 10,
        PostgreSQL = 1 * 10,
        SQLite = 2 * 10,
        MySQL = 3 * 10,
    }
}