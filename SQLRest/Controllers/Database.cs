using System;
using System.Data.SqlClient;
using System.Linq;

namespace SQLRest.Controllers
{
    public static class Database
    {
        public static BasicTableMetaData GetMetaData(this SqlConnection connection, string domain, string resource)
        {
            using (var reader = new DatabaseSchemaReader.DatabaseReader(connection))
            {
                var t = reader.AllTables().First(z => string.Equals(z.SchemaOwner , domain,StringComparison.OrdinalIgnoreCase) && string.Equals(z.Name,resource,StringComparison.OrdinalIgnoreCase));
                return new BasicTableMetaData()
                {
                    Name = resource,
                    Schema = domain,
                    PrimaryKeyName = t.PrimaryKeyColumn.Name
                };
            }
        }

        public class BasicTableMetaData
        {
            public string Name { get; set; }
            public string Schema { get; set; }
            public string PrimaryKeyName { get; set; }
        }
    }
}