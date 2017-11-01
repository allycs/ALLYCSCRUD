namespace Dapper
{
    using System;

    /// <summary>
    /// 可选的制度标签
    /// 你可以在system.componentmodel 指定属性上设置是否可编辑
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ReadOnlyAttribute : Attribute
    {
        /// <summary>
        /// 属性制度标签
        /// </summary>
        /// <param name="isReadOnly"></param>
        public ReadOnlyAttribute(bool isReadOnly)
        {
            IsReadOnly = isReadOnly;
        }

        /// <summary>
        /// 数据库是否沿用此属性标签
        /// </summary>
        public bool IsReadOnly { get; private set; }
    }
}