using Infrastructure;
using Microsoft.EntityFrameworkCore;
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
    }
}
