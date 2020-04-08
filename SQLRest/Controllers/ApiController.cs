using System.Collections.Generic;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DatabaseSchemaReader;
using Microsoft.AspNetCore.Mvc;

namespace SQLRest.Controllers
{
    [ApiController]
    public class ApiController : ControllerBase
    {
        private const string ConnectionString =
            "Server=TIMMDEV\\SQLEXPRESS2016;Database=SQLRestDemo;User Id=xx;Password=xx";
        
        [HttpGet]
        [Route("api")]
        public async Task<ActionResult<IEnumerable<Resource>>> Get()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var metaData = connection.GetMetaData();
                var resources = metaData.OrderBy(z => z.Schema).ThenBy(z => z.Name).Select(z => new Resource()
                {
                    Name = z.Name,
                    Domain = z.Schema,
                    Links = new Link[]
                    {
                      new Link()
                      {
                          Rel = "data", Href =  Url.Action("GetData", new { domain = z.Schema, resource = z.Name})
                      }  ,
                      new Link()
                      {
                          Rel = "metadata", Href =  Url.Action("GetMetaData", new { domain = z.Schema, resource = z.Name})
                      }  
                    },
                });
                return Ok(resources);
            }
        }
        
        [HttpGet]
        [Route("api/metadata/{domain}/{resource}")]
        public async Task<ActionResult<ResourceMetaData>> GetMetaData([FromRouteAttribute] string domain, [FromRouteAttribute] string resource)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var dbReader = new DatabaseReader(connection);
                var metaData = new ResourceMetaData()
                {
                    Name = resource,
                    Fields =  dbReader.AllTables().First(z=> z.Name == resource && z.SchemaOwner == domain).Columns.Select(z=> new Field()
                    {
                        Name = z.Name,
                        Type = z.IsForeignKey ? Url.Action("GetMetaData",new { domain = z.SchemaOwner, resource = z.Name}) : z.DataType.NetDataTypeCSharpName
                    }).ToArray()
                };
                return Ok(metaData);
            }
        }
        
        [HttpGet]
        [Route("api/data/{domain}/{resource}")]
        public async Task<ActionResult<IEnumerable<ExpandoObject>>> GetData([FromRouteAttribute] string domain, [FromRouteAttribute] string resource,[FromQuery] int count = 100)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var metaData = connection.GetMetaData(domain, resource);
                var rows = await connection.QueryAsync($"SELECT TOP {count} *  FROM {metaData.Schema}.{metaData.Name}");
                foreach (var row in rows)
                {
                    row.links = new Link[]
                    {
                        new Link()
                        {
                            Rel = "self",
                            Href = Url.Action("GetData", new {domain, resource, id = Reflection.GetPropertyValue(metaData.PrimaryKeyName,row) })
                        }
                    };
                }
                return Ok(rows.ToArray());
            }
        }
        
        [HttpGet]
        [Route("api/data/{domain}/{resource}/{id}")]
        public async Task<ActionResult<IEnumerable<ExpandoObject>>> GetData([FromRouteAttribute] string domain, [FromRouteAttribute] string resource,[FromRouteAttribute] string id)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var metaData =  connection.GetMetaData(domain,resource);
                var rows = await connection.QueryAsync($"SELECT *  FROM {metaData.Schema}.{metaData.Name} WHERE {metaData.PrimaryKeyName} = @id", new {id});
                return Ok(rows.ToArray());
            }
        }

        public class Link
        {
            public string Rel { get; set; }
            public string Href { get; set; }
        }

        public class Resource
        {
            public string Name { get; set; }
            public string Domain { get; set; }
            public Link[] Links { get; set; }
        }

        public class ResourceMetaData
        {
            public string Name { get; set; }
            public Field[] Fields { get; set; }
        }

        public class Field
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }
    }
}
