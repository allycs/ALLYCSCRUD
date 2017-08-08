namespace Dapper
{
    using System;

    /// <summary>
    /// 可选的列属性
    /// You can use the System.ComponentModel.DataAnnotations version in its place to specify the table name of a poco
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        /// <summary>
        /// 可选的列属性
        /// </summary>
        /// <param name="columnName"></param>
        public ColumnAttribute(string columnName)
        {
            Name = columnName;
        }

        /// <summary>
        /// 列名称
        /// </summary>
        public string Name { get; private set; }
    }
}