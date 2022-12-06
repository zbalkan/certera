using System;
using System.Linq;
using System.Threading.Tasks;
using Certera.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Certera.Web
{
    public static class WebHostExtensions
    {
        public static IHost InitializeDatabase(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetService<DataContext>();
                if (context == null)
                {
                    throw new AggregateException("DataContext service is null.");
                }

                var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

                // Run a DB migration if the DB doesn't exist or there are pending migrations
                var rdc = context?.Database?.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                var migrate = rdc != null && (!rdc.Exists() || context.Database.GetPendingMigrations().Any());

                if (migrate)
                {
                    context.Database.Migrate();
                }

                Task.Run(async () => {
                    var roleMgr = scope.ServiceProvider.GetService<RoleManager<Role>>();
                    if (!await roleMgr.RoleExistsAsync("Admin"))
                    {
                        await roleMgr.CreateAsync(new Role("Admin"));
                    }
                    if (!await roleMgr.RoleExistsAsync("User"))
                    {
                        await roleMgr.CreateAsync(new Role("User"));
                    }
                }).GetAwaiter().GetResult();
            }

            return host;
        }
    }
}