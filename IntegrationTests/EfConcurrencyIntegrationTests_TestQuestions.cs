using FluentResults;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Reflection;

namespace IntegrationTests
{
    [TestClass]
    public class EfConcurrencyIntegrationTests_TestQuestions : IntegrationTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            var optionsBuilder = new DbContextOptionsBuilder<EventsContext>();
            optionsBuilder.UseSqlServer(GetConnectionString(), b => b.MigrationsAssembly(typeof(EventsContext).GetTypeInfo().Assembly.GetName().Name));

            var db = CreateDbContext();

            db.Database.EnsureDeleted();

            db.Database.Migrate();
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
                var currentQuestions = concurrentQuestionRequests[i];
                var t = Task.Run(async () =>
                {
                    var svc = new QuestionsService(CreateDbContext());
                    var result = await svc.AddQuestionsForTestTransaction(1, currentQuestions, isolationLevel);
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
            }
            else
            {
                Assert.AreNotEqual(1, succeededRuns.Count());
            }
        }

        [TestMethod]
        public async Task MultipleThreadsTryAddQuestionsToSameTestId_RowVersion_OnlyOneShouldAdd_ShouldFail()
        {
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
                var currentQuestions = concurrentQuestionRequests[i];
                var t = Task.Run(async () =>
                {
                    var svc = new QuestionsService(CreateDbContext());
                    var result = await svc.AddQuestionsForTestRowVersioned(1, currentQuestions);
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

            var failed = false;
            try
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
            }
            catch (AssertFailedException assertFailedEx)
            {
                failed = true;
            }
            Assert.IsTrue(failed);
        }
    }
}
