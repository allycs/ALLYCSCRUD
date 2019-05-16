namespace Dapper
{
    using System.Collections.Generic;
    using System.Data;

    ///<summary>
    ///</summary>
    public class Populate
    {
        ///<summary>
        ///</summary>
        ///<param name="dr"></param>
        ///<typeparam name="T"></typeparam>
        ///<returns></returns>
        public T GetSingle<T>(IDataReader dr)
        {
            if (dr.Read())
            {
                var obj = DynamicBuilder<T>.CreateBuilder(dr).Build(dr);
                if (!dr.IsClosed)
                    dr.Close();
                return obj;
            }
            if (!dr.IsClosed)
                dr.Close();
            return default(T);
        }

        ///<summary>
        ///</summary>
        ///<param name="dr"></param>
        ///<typeparam name="T"></typeparam>
        ///<returns></returns>
        public List<T> GetList<T>(IDataReader dr)
        {
            var list = new List<T>();
            var builder = DynamicBuilder<T>.CreateBuilder(dr);
            while (dr.Read())
            {
                list.Add(builder.Build(dr));
            }
            dr.Close();
            return list;
        }

        internal static Populate Instance() => new Populate();
    }
}