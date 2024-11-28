using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Infrastructure
{
    public class QuestionsService
    {
        private readonly EventsContext _eventsContext;

        public QuestionsService(EventsContext eventsContext)
        {
            _eventsContext = eventsContext;
        }

        public async Task<Result<List<TestQuestion>>> AddQuestionsForTestTransaction(int testId, List<string> texts, IsolationLevel? isolationLevel)
        {
            try
            {
                if (isolationLevel.HasValue)
                {
                    using var ts = await _eventsContext.Database.BeginTransactionAsync(isolationLevel.Value);
                    return (await AddQuestionsForTest(testId, texts, ts));
                }
                return (await AddQuestionsForTest(testId, texts, null));
            }
            catch (Exception ex)
            {
                return Result.Fail(ex.Message);
            }
        }

        private async Task<Result<List<TestQuestion>>> AddQuestionsForTest(int testId, List<string> texts, IDbContextTransaction? transaction)
        {
            var existing = await _eventsContext.TestQuestions.AnyAsync(q => q.TestId == testId);

            if (existing)
            {
                return Result.Fail($"{nameof(TestQuestion)}s for {nameof(testId)}={testId} already exists");
            }
            await Task.Delay(new Random().Next(0, 10));
            var toAdd = texts.Select(t => new TestQuestion
            {
                TestId = testId,
                Text = t,
            }).ToList();
            await _eventsContext.TestQuestions.AddRangeAsync(toAdd);
            await Task.Delay(new Random().Next(0, 10));
            await _eventsContext.SaveChangesAsync();
            transaction?.CommitAsync();
            return Result.Ok(toAdd);
        }

        public async Task<Result<List<TestQuestion>>> AddQuestionsForTestRowVersioned(int testId, List<string> texts)
        {
            try
            {
                var existing = await _eventsContext.TestQuestions.AnyAsync(q => q.TestId == testId);

                if (existing)
                {
                    return Result.Fail($"{nameof(TestQuestion)}s for {nameof(testId)}={testId} already exists");
                }
                var toAdd = texts.Select(t => new TestQuestion
                {
                    TestId = testId,
                    Text = t,
                }).ToList();
                await _eventsContext.TestQuestions.AddRangeAsync(toAdd);
                await Task.Delay(new Random().Next(0, 10));
                await _eventsContext.SaveChangesAsync();
                return toAdd;
            }
            catch (Exception ex)
            {
                return Result.Fail(ex.Message);
            }
        }
    }
}
