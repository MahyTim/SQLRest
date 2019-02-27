using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace SQLRest.Controllers
{
    [Route("[controller]")]
    [GenericControllerNameAttribute]
    public class GenericController<T> : Controller
    {
        private const string ConnectionString =
            "Server=.;Database=test;User Id=sa;Password=yourStrong(!)Password";

        [HttpGet]
        public async Task<IActionResult> IndexAsync()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var rows = await connection.QueryAsync<T>($"SELECT TOP 100 * FROM Animals");
                return Json(rows);
            }
        }

        [HttpPost]
        public IActionResult Create([FromBody] IEnumerable<T> items)
        {
            return Content($"POST to a {typeof(T).Name} controller.");
        }
    }

    public class GenericControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            // Get the list of entities that we want to support for the generic controller
            foreach (var entityType in IncludedEntities.Types)
            {
                var typeName = entityType.Name + "Controller";

                // Check to see if there is a "real" controller for this class
                if (!feature.Controllers.Any(t => t.Name == typeName))
                {
                    // Create a generic controller for this type
                    var controllerType = typeof(GenericController<>).MakeGenericType(entityType.AsType()).GetTypeInfo();
                    feature.Controllers.Add(controllerType);
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class GenericControllerNameAttribute : Attribute, IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            if (controller.ControllerType.GetGenericTypeDefinition() == typeof(GenericController<>))
            {
                var entityType = controller.ControllerType.GenericTypeArguments[0];
                controller.ControllerName = entityType.Name;
            }
        }
    }

    public static class IncludedEntities
    {
        public static IReadOnlyList<TypeInfo> Types;

        static IncludedEntities()
        {
            var assembly = typeof(IncludedEntities).GetTypeInfo().Assembly;
            var typeList = new List<TypeInfo>();

            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(ApiEntityAttribute), true).Length > 0)
                {
                    typeList.Add(type.GetTypeInfo());
                }
            }

            Types = typeList;
        }
    }

    [ApiEntityAttribute]
    public class Animals
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    [ApiEntityAttribute]
    public class Insects
    {
        public string X { get; set; }
        public string Z { get; set; }
    }

    /// <summary>
    /// This is just a marker attribute used to allow us to identifier which entities to expose in the API
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ApiEntityAttribute : Attribute
    {
    }
}