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
                var query = Sql.DataAccess.CreateStored("[auth].[adduser]")
                    .SetStringParam("userName", user.UserName)
                    .SetStringParam("name", user.Name)
                    .SetStringParam("surname", user.Surname)
                    .SetStringParam("email", user.Email)
                    .SetInt32OutParam("identity");
                query.ExecuteNonQuery();
                user.Id = query.GetOutParam<Int32>("identity");
            }
        }

        public List<User> GetAll()
        {
            return Sql.DataAccess.CreateStored("[auth].[GetUsers]")
                .Hydrate<User>();
        }
    }
}
