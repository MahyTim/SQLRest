using System;
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
    public class APIController : ControllerBase
    {
        private const string ConnectionString =
            "Server=TIMMDEV\\SQLEXPRESS2016;Database=unipass;User Id=unipass;Password=uniPass1";
        
        [HttpGet]
        [Route("api")]
        public async Task<ActionResult<IEnumerable<Resource>>> Get()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var dbReader = new DatabaseReader(connection);

                var resources = dbReader.AllTables().OrderBy(z => z.SchemaOwner).ThenBy(z => z.Name).Select(z => new Resource()
                {
                    Name = z.Name,
                    Domain = z.SchemaOwner,
                    MetaDataUrl = Url.Action("GetMetaData", new { domain = z.SchemaOwner, resource = z.Name}),
                    DataUrl = Url.Action("GetData", new { domain = z.SchemaOwner, resource = z.Name}),
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
        public async Task<ActionResult<IEnumerable<ExpandoObject>>> GetData([FromRouteAttribute] string domain, [FromRouteAttribute] string resource)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var rows = await connection.QueryAsync($"SELECT *  FROM {domain}.{resource}");
                return Ok(rows.ToArray());
            }
        }

        public class Resource
        {
            public string Name { get; set; }
            public string Domain { get; set; }
            public string MetaDataUrl { get; set; }
            public string DataUrl { get; set; }
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