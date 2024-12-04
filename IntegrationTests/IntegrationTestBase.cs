using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Respawn;
using System.Reflection;
using Testcontainers.MsSql;

namespace IntegrationTests
{
    [TestClass]
    public abstract class IntegrationTestBase
    {
        protected static MsSqlContainer _msSqlContainer = new MsSqlBuilder().WithPortBinding(1433).Build();

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext testContext)
        {
            int tries = 0;
            bool success = false;
            do
            {
                try
                {
                    _msSqlContainer.StartAsync().GetAwaiter().GetResult();
                    success = true;
                }
                catch
                {
                    tries++;
                    if (tries > 3)
                    {
                        throw;
                    }
                    Thread.Sleep(1000);
                }
            } while (!success);
            InitializeDb();
        }

        private static void InitializeDb()
        {
            var optionsBuilder = new DbContextOptionsBuilder<EventsContext>();
            optionsBuilder.UseSqlServer(GetConnectionString(), b => b.MigrationsAssembly(typeof(EventsContext).GetTypeInfo().Assembly.GetName().Name));
            var db = new EventsContext(optionsBuilder.Options);
            db.Database.EnsureDeleted();
            db.Database.Migrate();
        }

        // Doesn't seem to be necessary?
        //[AssemblyCleanup]
        //public static void AssemblyCleanup()
        //{
        //    _msSqlContainer.DisposeAsync().GetAwaiter().GetResult();
        //}

        protected static string GetConnectionString()
        {
            return _msSqlContainer.GetConnectionString().Replace("master", "events");
        }

        protected static EventsContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<EventsContext>();
            optionsBuilder.UseSqlServer(GetConnectionString());
            return new EventsContext(optionsBuilder.Options);
        }

        protected static async Task ResetDbAsync()
        {
            var respawner = await Respawner.CreateAsync(GetConnectionString(), new RespawnerOptions
            {
                WithReseed = true
            });
            await respawner.ResetAsync(GetConnectionString());
        }
    }
}
