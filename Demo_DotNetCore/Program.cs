using Dapper;
using System;

namespace Demo_DotNetCore
{
    class Program
    {
        static void Main(string[] args)
        {
            var token = "token";
            var userId = "userId";
            var members = DbHelper.CreateConnection().GetList<Member>("dt_customer", new { token = token, uid = userId });
            foreach (var item in members)
            {
                Console.WriteLine(item.Nickname);
            }
            Console.ReadLine();
        }
    }
}
