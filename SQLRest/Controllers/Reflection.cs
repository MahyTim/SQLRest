using System;
using System.Collections.Generic;
using System.Linq;
using FastMember;

namespace SQLRest.Controllers
{
    public static class Reflection
    {
        public static string GetPropertyValue(string propertyName, object obj)
        {
            if (obj is ICollection<KeyValuePair<string, object>> easyAccessible)
            {
                return Convert.ToString(easyAccessible.FirstOrDefault(z => string.Equals(z.Key, propertyName, StringComparison.OrdinalIgnoreCase)).Value);
            }
            
            var accessor = TypeAccessor.Create(obj.GetType());
            return Convert.ToString(accessor[obj, propertyName]);
        }
    }
}