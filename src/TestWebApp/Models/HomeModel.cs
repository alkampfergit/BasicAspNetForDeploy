using System;
using System.Collections.Generic;

namespace TestWebApp.Models
{
    public class HomeModel
    {
        public HomeModel()
        {
            Parameters = new List<Parameter>();
        }

        public IList<Parameter> Parameters { get; set; }
    }

    public class Parameter
    {
        public Parameter(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public String Key { get; set; }

        public String Value { get; set; }
    }
}