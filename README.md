# Dapper.ALLYCSCRUD - 基于Dapper的应用级扩展

- 中文文档待补充；代码全中文注释，等不及可以直接看代码
## 此扩展有以下几种方法：
- Get(id) - 根据主键获取对象

- GetList\<Type\>()  根据类型获取表中所有数据

- GetList\<Type\>(不记名对象作为限定条件WHERE 例：new { Age = 15 },非必传表名（可使用标签代替，此处有额外用处自行体会）)  根据限定条件获取表中符合的对象列表

- GetList\<Type\>(string的where条件例：WHERE name='bob',非必传表名（可使用标签代替，此处有额外用处自行体会））  根据限定条件获取表中符合的对象列表

- GetListPaged\<Type\>GetListPagedAsync<T>(页码从1开始, 每页数据量, string类型的限定条件"WHERE name='bob' ", string类型的排序"age desc", 非必传表名)  根据限定条件获取自定义分页数据
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
