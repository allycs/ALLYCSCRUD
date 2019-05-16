using Dapper;
using System;

namespace Demo_DotNetCore
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var members = DbHelper.CreateConnection().GetList<Member>(new { token = "Token", uid = "UserId" }, "dt_customer");
            foreach (var item in members)
            {
                Console.WriteLine(item.Nickname);
            }
            Console.ReadLine();
        }
    }
}