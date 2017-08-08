namespace Dapper
{
    using System;

    /// <summary>
    /// Optional Table attribute.
    /// You can use the System.ComponentModel.DataAnnotations version in its place to specify the table name of a poco
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        /// <summary>
        /// 设置表名标签
        /// </summary>
        /// <param name="tableName"></param>
        public TableAttribute(string tableName)
        {
            Name = tableName;
        }

        /// <summary>
        /// 表名
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 视图名
        /// </summary>
        public string Schema { get; set; }
    }
}