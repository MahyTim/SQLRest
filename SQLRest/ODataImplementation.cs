using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlClient;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using DatabaseSchemaReader;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;
using BindingFlags = System.Reflection.BindingFlags;

namespace SQLRest
{
    
    
    public static class ODataImplementation
    {
        public static void ConfigureOData(this IServiceCollection services)
        {
            services.AddOData();

            var dbContextType = GenerateClasses().Where(z => z.IsSubclassOf(typeof(DbContext))).First();

            var optionsBuilder = new System.Action<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder>(z=> {});
            
            typeof(EntityFrameworkServiceCollectionExtensions).GetMethods().Where(z=> z.Name == "AddDbContext" && z.IsStatic && z.IsGenericMethod && z.GetParameters().Length == 4).First()
                .MakeGenericMethod(dbContextType).Invoke(null, new Object []
                {
                    services,
                    optionsBuilder,
                    ServiceLifetime.Scoped,
                    ServiceLifetime.Scoped,
                });
        }

        private static IEdmModel GetEdmModel()
        {
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();

            var entities = GenerateClasses().Where(z => false == z.IsSubclassOf(typeof(DbContext)));
            foreach (var entity in entities)
            {
                builder.GetType().GetMethod("EntitySet").MakeGenericMethod(entity).Invoke(builder, new[] {entity.Name});
            }
            return builder.GetEdmModel();
        }


        private static List<Type> GeneratedTypes = new List<Type>();

        private static IEnumerable<Type> GenerateClasses()
        {
            if (GeneratedTypes.Any())
                return GeneratedTypes;

            var generatedTypeNames = new List<string>();
            
            using (var connection = new SqlConnection(Settings.ConnectionString))
            {
                var reader = new DatabaseReader(connection);

                var dbContextBuilder = new StringBuilder();
                
                generatedTypeNames.Add("Entities.DataContext");
                dbContextBuilder.AppendLine("public class DataContext : DbContext");
                dbContextBuilder.AppendLine("{ // class");
                dbContextBuilder.AppendLine("public DataContext(DbContextOptions<DataContext> options) : base(options){}");


                var entitiesBuilder = new StringBuilder();
                entitiesBuilder.AppendLine(@"using System;");
                entitiesBuilder.AppendLine(@"using System.Collections.Generic;");
                entitiesBuilder.AppendLine(@"using System.ComponentModel.DataAnnotations.Schema;");
                entitiesBuilder.AppendLine(@"using System.ComponentModel.DataAnnotations;");
                entitiesBuilder.AppendLine("using Microsoft.EntityFrameworkCore;");
                entitiesBuilder.AppendLine();
                entitiesBuilder.AppendLine("namespace Entities");
                entitiesBuilder.AppendLine("{");

                foreach (var table in reader.AllTables())
                {
                    generatedTypeNames.Add($"Entities.{table.Name}" );
                    
                    dbContextBuilder.AppendLine($"public DbSet<{table.Name}> {table.Name} {{get;set;}}");

                    entitiesBuilder.AppendLine($"[TableAttribute(\"{table.Name}\", Schema = \"{table.SchemaOwner}\")]");
                    entitiesBuilder.AppendLine($"public class {table.Name}");
                    entitiesBuilder.AppendLine("{");
                    foreach (var column in table.Columns)
                    {
                        if (column.IsPrimaryKey)
                        {
                            entitiesBuilder.AppendLine("[KeyAttribute]");
                        }

                        entitiesBuilder.AppendLine(
                            $@"public {column.DataType.NetDataTypeCSharpName} {column.Name} {{get; set;}}");
                    }

                    entitiesBuilder.AppendLine("} // class");
                }

                dbContextBuilder.AppendLine("} // class");
                entitiesBuilder.AppendLine(dbContextBuilder.ToString());
                entitiesBuilder.AppendLine("} // namespace");

                var syntaxTree =
                    CSharpSyntaxTree.ParseText(entitiesBuilder.ToString());

                var trustedAssembliesPaths =
                    ((string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
                var neededAssemblies = new[]
                {
                    "System.Runtime",
                    "mscorlib",
                    "System.Private.CoreLib",
                    "netstandard"
                };
                var references = trustedAssembliesPaths
                    .Where(p => neededAssemblies.Contains(Path.GetFileNameWithoutExtension((string) p)))
                    .Select(p => MetadataReference.CreateFromFile(p))
                    .ToList();
                references.Add(MetadataReference.CreateFromFile(typeof(KeyAttribute).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(TableAttribute).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location));

                CSharpCompilation compilation = CSharpCompilation.Create(
                    "assemblyName",
                    new[] {syntaxTree},
                    references.ToArray(),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using (var dllStream = new MemoryStream())
                using (var pdbStream = new MemoryStream())
                {
                    var emitResult = compilation.Emit(dllStream, pdbStream);
                    dllStream.Position = pdbStream.Position = 0;
                    if (!emitResult.Success)
                    {
                        throw new Exception(emitResult.Diagnostics.First().GetMessage());
                    }
                    else
                    {
                        var myAssembly = AssemblyLoadContext.Default.LoadFromStream(dllStream, pdbStream);
                        foreach (var generatedTypeName in generatedTypeNames)
                        {
                            GeneratedTypes.Add(myAssembly.GetType(generatedTypeName) ?? throw new NotSupportedException(generatedTypeName));
                        }
                        return GeneratedTypes;
                    }
                }
            }
        }

        public static void ConfigureMvc(IRouteBuilder routeBuilder)
        {
            routeBuilder.Select().Expand().Filter().OrderBy().MaxTop(100).Count();
            routeBuilder.MapODataServiceRoute("odata", "odata", GetEdmModel());
            routeBuilder.EnableDependencyInjection();   
        }
    }
}