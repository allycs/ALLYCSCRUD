# Dapper.ALLYCSCRUD - 基于Dapper的应用级扩展

- 中文文档待补充代码全中文注释等不及可以直接看代码

 ## Get a single record mapped to a strongly typed object
`
 public static T Get<T>(this IDbConnection connection, int id)
`
 ### Example basic usage:
```
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
} 
var user = connection.Get<User>(1);  
 ```    
## Results in executing this SQL

`Select Id, Name, Age from [User] where Id = 1 `
### More complex example:
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
## Results in executing this SQL
`
Select UserId, strFirstName as FirstName, LastName, Age from [Users] where UserId = @UserID
`
