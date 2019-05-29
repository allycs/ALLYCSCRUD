# Dapper.ALLYCSCRUD - 基于Dapper的实战应用级扩展

- 支持数据库以及schema用法在程序启动后写入该句
```
三个参数依次：数据库类型，schema名，是否大小写转换（AaBb转aa_bb）
AllycsCRUD.SetDBType(DBType.PostgreSQL, "truckinfo",true);
```
## 此扩展有以下几种方法（详细可看代码注释）：
- Get(id) - 根据主键获取对象
- GetList\<Type\>()  根据类型获取表中所有数据
- GetList\<Type\>(不记名对象作为限定条件WHERE 例：new { Age = 15 },非必传表名（可使用标签代替，此处有额外用处自行体会）)  根据限定条件获取表中符合的对象列表
- GetList\<Type\>(string的where条件例：WHERE name='bob',非必传表名（可使用标签代替，此处有额外用处自行体会））  根据限定条件获取表中符合的对象列表
- GetListPaged\<Type\>GetListPagedAsync<T>(页码从1开始, 每页数据量, string类型的限定条件"WHERE name='bob' ", string类型的排序"age desc", 非必传表名)  根据限定条件获取自定义分页数据
- Insert\<TKey\>(数据对象, 非必传表明)  注意\<TKey\>不是泛型例如：如果是自增则 conn.InsertAsync<int>(New User{}) 返回插入对象的主键值
- Insert<T>(T entity) 对象插入包含主键值 返回是否成功
- Update(entity) - 更新数据对象
- Delete<Type>(id) - 根据主键删除
- Delete(entity) - 根据数据对象删除
- DeleteList\<Type\>(不记名对象作为限定条件WHERE 例：new { Age = 15 })  根据限定条件删除
- DeleteList\<Type\>(string的where条件例：WHERE name='bob')  根据限定条件删除
- RecordCount\<Type\>() 统计条数
- RecordCount\<Type\>(不记名对象作为限定条件WHERE 例：new { Age = 15 }) 统计条数
- RecordCount\<Type\>(string的where条件例：WHERE name='bob') 统计条数

### 扩展方法的异步（支持.NET 4.5 以上以及 .NET CORE ）

- GetAsync(id) - 根据主键获取对象
- GetListAsync\<Type\>()  根据类型获取表中所有数据
- GetListAsync\<Type\>(不记名对象作为限定条件WHERE 例：new { Age = 15 },非必传表名（可使用标签代替，此处有额外用处自行体会）)  根据限定条件获取表中符合的对象列表
- GetListAsync\<Type\>(string的where条件例：WHERE name='bob',非必传表名（可使用标签代替，此处有额外用处自行体会））  根据限定条件获取表中符合的对象列表
- GetListPagedAsync\<Type\>GetListPagedAsync<T>(页码从1开始, 每页数据量, string类型的限定条件"WHERE name='bob' ", string类型的排序"age desc", 非必传表名)  根据限定条件获取自定义分页数据
- InsertAsync\<TKey\>(数据对象, 非必传表明)  注意\<TKey\>不是泛型例如：如果是自增则 conn.InsertAsync<int>(New User{}) 返回插入对象的主键值
- InsertAsync<T>(T entity) 对象插入包含主键值 返回是否成功
- UpdateAsync(entity) - 更新数据对象
- DeleteAsync<Type>(id) - 根据主键删除
- DeleteAsync(entity) - 根据数据对象删除
- DeleteListAsync\<Type\>(不记名对象作为限定条件WHERE 例：new { Age = 15 })  根据限定条件删除
- DeleteListAsync\<Type\>(string的where条件例：WHERE name='bob')  根据限定条件删除
- RecordCountAsync\<Type\>() 统计条数
- RecordCountAsync\<Type\>(不记名对象作为限定条件WHERE 例：new { Age = 15 }) 统计条数
- RecordCountAsync\<Type\>(string的where条件例：WHERE name='bob') 统计条数
 
 ## 使用方式
 
1. 构建DbHelper类

   ```
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
   ```
  
2. 根据对象和相应条件方法获取数据，哪里用哪里取
   
   ```
     public class MemberInfo
     {
         public int Id { get; set; }
         public string Name { get; set; }
         public int Age { get; set; }
     } 
   ``` 
 
 > 若配置默认（大小写转换AaBb=>aa_bb）表名默认
 
  ` var members = DbHelper.CreateConnection().GetList<Member>(new { Name = "猜测", Age = 23 }); `
 
 > 表名与类名不符或不能默认大小写转换;假定数据库表名为： "dt_customer" 
   
   1. 方式1：

   ` var members = DbHelper.CreateConnection().GetList<Member>(new { Name = "猜测", Age = 23  , "dt_customer"}); `

   2. 方式2：加attribute方式
   
   ```
     [Table("dt_customer")]
     public class MemberInfo
     {
         public int Id { get; set; }
         public string Name { get; set; }
         public int Age { get; set; }
     } 
     var members = DbHelper.CreateConnection().GetList<Member>(new { Name = "猜测", Age = 23 });
   ```
 3. 表中的属性名称的使用方式与表名类似：[Column("strFirstName")]
   
 ## 根据ID获取对象
`
 public static T Get<T>(this IDbConnection connection, int id)
`
 ### 基础案例（仅案例展示用）:
```
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
} 
var user = connection.Get<User>(1);  
 ```    
## 实际的SQL语句为：

`Select Id, Name, Age from [User] where Id = 1 `
### 自定义表名使用方式:
```
    [Table("Users")]
    public class User
    {
        [Key]
        public int UserId { get; set; }
        [Column("strFirstName")]
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
    }
    
    var user = connection.Get<User>(1);  
```
## 实际的SQL语句
`
Select UserId, strFirstName as FirstName, LastName, Age from [Users] where UserId = @UserID
`
