using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWebApp.Core.Sql
{
    internal class OutputParameter
    {
        public Object Value { get; set; }
        public String Name { get; set; }
        public Type Type { get; set; }

        public OutputParameter(string name, Type type)
        {
            Name = name;
            Type = type;
        }
    }
}
