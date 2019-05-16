namespace Dapper
{
    using System;

    /// <summary>
    /// 可选的列属性
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