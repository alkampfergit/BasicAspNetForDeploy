using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWebApp.Core.DbEntities
{
    public class User
    {
        public Int32 Id { get; set; }

        public String UserName { get; set; }

        public String Name { get; set; }

        public String Surname { get; set; }

        public String Email { get; set; }
    }
}
