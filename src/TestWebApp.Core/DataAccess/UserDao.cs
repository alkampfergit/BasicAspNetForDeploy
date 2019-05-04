using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestWebApp.Core.DbEntities;

namespace TestWebApp.Core.DataAccess
{
    public class UserDao
    {
        public void UpsertUser(User user)
        {
            if (user.Id > 0)
            {
                throw new NotImplementedException();
            }
            else
            {
                var newId = Sql.DataAccess.CreateQuery(@"
INSERT INTO [dbo].[Users]
           ([UserName]
           ,[Name]
           ,[Surname]
           ,[email])
     VALUES
           (@username
           ,@name
           ,@surname
           ,@email);
SELECT SCOPE_IDENTITY(); 
")
                 .SetStringParam("userName", user.UserName)
                 .SetStringParam("name", user.Name)
                 .SetStringParam("surname", user.Surname)
                 .SetStringParam("email", user.Email)
                 .ExecuteScalar<Decimal>();
                user.Id = (Int32) newId;
            }
        }
    }
}
