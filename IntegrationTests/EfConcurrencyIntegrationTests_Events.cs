using Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace IntegrationTests
{
    [TestClass]
    public class EfConcurrencyIntegrationTests_Events : IntegrationTestBase
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await ResetDbAsync();

            var db = CreateDbContext();
            DataSeeder.Seed(db);
        }

        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow(IsolationLevel.ReadUncommitted, false)]
        [DataRow(IsolationLevel.ReadCommitted, false)]
        [DataRow(IsolationLevel.Snapshot, true)]
        [DataRow(IsolationLevel.RepeatableRead, true)]
        [DataRow(IsolationLevel.Serializable, true)]
        public async Task MultipleThreadsTryBookingSameSeat_OnlyOneShouldBook(IsolationLevel? isolationLevel, bool shouldPass)
        {
            if (isolationLevel == IsolationLevel.Snapshot)
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
                    var ticket = await svc.BookSeatTransaction(1, 1, 1, isolationLevel);
                    return ticket;
                });
                tasks.Add(t);
            };

            await Task.WhenAll(tasks);
            var tickets = tasks.Select(t => t.Result).ToList();
            var nonNullTickets = tickets.Where(s => s.Item1 != null).ToList();
            if (shouldPass)
            {
                Assert.AreEqual(1, nonNullTickets.Count());
                Assert.AreEqual(1, CreateDbContext().Tickets.Count());
            }
            else
            {
                Assert.AreNotEqual(1, nonNullTickets.Count());
                Assert.AreNotEqual(1, CreateDbContext().Tickets.Count());
            }
        }

        [TestMethod]
        public async Task MultipleThreadsTryBookingSameSeat_OnlyOneShouldBook_RowVersion()
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
            Assert.AreEqual(1, CreateDbContext().TicketsRowVersion.Count());
        }
    }
}