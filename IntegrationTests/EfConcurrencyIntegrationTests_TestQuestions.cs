using FluentResults;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using Testcontainers.MsSql;

namespace IntegrationTests
{
    [TestClass]
    public class EfConcurrencyIntegrationTests_TestQuestions
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

        private string GetConnectionString()
        {
            return _msSqlContainer.GetConnectionString().Replace("master", "events");
        }
        private EventsContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<EventsContext>();
            optionsBuilder.UseSqlServer(GetConnectionString());
            return new EventsContext(optionsBuilder.Options);
        }

        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow(IsolationLevel.ReadUncommitted, false)]
        [DataRow(IsolationLevel.ReadCommitted, false)]
        [DataRow(IsolationLevel.Snapshot, false)]
        [DataRow(IsolationLevel.RepeatableRead, false)]
        [DataRow(IsolationLevel.Serializable, true)]
        public async Task MultipleThreadsTryAddQuestionsToSameTestId_OnlyOneShouldAdd(IsolationLevel? isolationLevel, bool shouldPass)
        {
            if (isolationLevel == IsolationLevel.Snapshot)
            {
                CreateDbContext().Database.ExecuteSqlRaw("ALTER DATABASE [events] SET ALLOW_SNAPSHOT_ISOLATION ON");
            }
            else
            {
                CreateDbContext().Database.ExecuteSqlRaw("ALTER DATABASE [events] SET ALLOW_SNAPSHOT_ISOLATION OFF");
            }

            var concurrentQuestionRequests = new List<List<string>>();
            for (int i = 0; i < 10; i++)
            {
                var minNumber = Math.Max(2, i);

                var iterationList = new List<string>();
                for (int j = 0; j < minNumber; j++)
                {
                    iterationList.Add($"Question {j}- iter: {i}");
                }
                concurrentQuestionRequests.Add(iterationList);
            }

            var tasks = new List<Task<Result<List<TestQuestion>>>>();
            for (int i = 0; i < 10; i++)
            {
                var temp = concurrentQuestionRequests[i];
                var t = Task.Run(async () =>
                {
                    var svc = new QuestionsService(CreateDbContext());
                    var result = await svc.AddQuestionsForTestTransaction(1, temp, isolationLevel);
                    return result;
                });
                tasks.Add(t);
            };

            await Task.WhenAll(tasks);

            var allSucceeded = CreateDbContext().TestQuestions
                .Select(q => q.Text)
                .ToList();

            var succeededRuns = tasks.Select(x => x.Result).Where(x => x.IsSuccess).ToList();
            var failedRuns = tasks.Select(x => x.Result).Where(x => x.IsFailed).ToList();
            //var tickets = tasks.Select(t => t.Result).ToList();
            //var nonNullTickets = tickets.Where(s => s.Item1 != null).ToList();
            if (shouldPass)
            {
                Assert.AreEqual(1, succeededRuns.Count());
                Assert.AreEqual(9, failedRuns.Count());
                var firstInserted = allSucceeded[0];
                var firstIteration = firstInserted.Split("-")[1];
                for (int i = 0; i < allSucceeded.Count; i++)
                {
                    var iteration = allSucceeded[i].Split("-")[1];
                    Assert.AreEqual(firstIteration, iteration);
                }
                //Assert.AreEqual(1, db.Tickets.Count());
            }
            else
            {
                Assert.AreNotEqual(1, succeededRuns.Count());
                //Assert.AreNotEqual(1, db.Tickets.Count());
            }
        }
    }
}
