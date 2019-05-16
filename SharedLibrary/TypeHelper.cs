namespace Dapper
{
    using System.Reflection;
    using System.Text;

    public static class TypeHelper
    {
        public static string GetFieldName(PropertyInfo p)
        {
            var chars = p.Name.ToCharArray();
            var sb = new StringBuilder();
            sb.Append(char.ToLower(chars[0]));
            if (chars.Length > 1)
            {
                for (int i = 1; i < chars.Length; i++)
                {
                    var c = chars[i];
                    if (char.IsUpper(c) && !char.IsDigit(c))
                    {
                        sb.Append("_" + char.ToLower(c));
                    }
                    else
                    {
                        sb.Append(char.ToLower(c));
                    }
                }
            }
            return sb.ToString();
        }
    }
}