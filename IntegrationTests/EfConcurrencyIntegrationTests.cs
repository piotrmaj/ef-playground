using Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Reflection;
using Testcontainers.MsSql;

namespace IntegrationTests
{
    [TestClass]
    public class EfConcurrencyIntegrationTests
    {
        private static MsSqlContainer _msSqlContainer = new MsSqlBuilder().WithPortBinding(1433).Build();
        private EventsContext db;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
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

        [TestInitialize]
        public void TestInitialize()
        {
            var optionsBuilder = new DbContextOptionsBuilder<EventsContext>();
            optionsBuilder.UseSqlServer(GetConnectionString(), b => b.MigrationsAssembly(typeof(EventsContext).GetTypeInfo().Assembly.GetName().Name));

            db = new EventsContext(optionsBuilder.Options);

            db.Database.EnsureDeleted();

            db.Database.Migrate();
            DataSeeder.Seed(db);
        }

        private EventsContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<EventsContext>();
            optionsBuilder.UseSqlServer(GetConnectionString());
            return new EventsContext(optionsBuilder.Options);
        }

        private string GetConnectionString()
        {
            return _msSqlContainer.GetConnectionString().Replace("master", "events");
        }

        //[ClassCleanup]
        //public static void ClassCleanup(TestContext testContext)
        //{
        //    //_msSqlContainer.DisposeAsync().GetAwaiter().GetResult();
        //}

        [TestCleanup]
        public void TestCleanup()
        {
            //_msSqlContainer.DisposeAsync().GetAwaiter().GetResult();
        }

        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow(IsolationLevel.ReadUncommitted, false)]
        [DataRow(IsolationLevel.ReadCommitted, false)]
        [DataRow(IsolationLevel.Snapshot, true)]
        [DataRow(IsolationLevel.RepeatableRead, true)]
        [DataRow(IsolationLevel.Serializable, true)]
        public async Task MultipleThreadsTryBookingSameSeat_OnlyOneShoultBook(IsolationLevel? isolationLevel, bool shouldPass)
        {
            if(isolationLevel == IsolationLevel.Snapshot)
            {
                CreateDbContext().Database.ExecuteSqlRaw("ALTER DATABASE [events] SET ALLOW_SNAPSHOT_ISOLATION ON");
            }
            else
            {
                CreateDbContext().Database.ExecuteSqlRaw("ALTER DATABASE [events] SET ALLOW_SNAPSHOT_ISOLATION OFF");
            }

            var tasks = new List<Task<(Ticket, string)>>();
            for (int i = 0; i < 10; i++)
            {
                var t = Task.Run(async () =>
                {
                    var svc = new BookService(CreateDbContext());
                    var ticket =  await svc.BookSeatTransaction(1, 1, 1, isolationLevel);
                    return ticket;
                });
                tasks.Add(t);
            };

            await Task.WhenAll(tasks);
            var tickets = tasks.Select(t => t.Result).ToList();
            var nonNullTickets = tickets.Where(s => s.Item1 != null).ToList();
            if(shouldPass)
            {
                Assert.AreEqual(1, nonNullTickets.Count());
                //Assert.AreEqual(1, db.Tickets.Count());
            } 
            else
            {
                Assert.AreNotEqual(1, nonNullTickets.Count());
                //Assert.AreNotEqual(1, db.Tickets.Count());
            }
        }

        [TestMethod]
        public async Task MultipleThreadsTryBookingSameSeat_OnlyOneShoultBook_RowVersion()
        {
            var tasks = new List<Task<(TicketRowVersion, string)>>();
            for (int i = 0; i < 10; i++)
            {
                var t = Task.Run(async () =>
                {
                    var svc = new BookService(CreateDbContext());
                    var ticket = await svc.BookSeatTransactionRowVersioned(1, 1, 1);
                    return ticket;
                });
                tasks.Add(t);
            };

            await Task.WhenAll(tasks);
            var tickets = tasks.Select(t => t.Result).ToList();
            var nonNullTickets = tickets.Where(s => s.Item1 != null).ToList();
            Assert.AreEqual(1, nonNullTickets.Count());
        }
    }
}