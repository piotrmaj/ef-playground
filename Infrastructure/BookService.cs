using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure
{
    public class BookService
    {
        private readonly EventsContext _eventsContext;

        public BookService(EventsContext eventsContext)
        {
            _eventsContext = eventsContext;
        }

        public async Task<Ticket> BookSeat(long eventId, int row, int number)
        {
            var seat = await _eventsContext.Seats.FirstAsync(s =>
                s.Event.Id == eventId
                && s.Row == row
                && s.Number == number);
            if (seat.Ticket == null)
            {
                seat.Ticket = new Ticket();
            }
            await _eventsContext.SaveChangesAsync();
            return seat.Ticket;
        }

        public async Task<(Ticket, string)> BookSeatTransaction(long eventId, int row, int number, IsolationLevel? isolationLevel)
        {
            try
            {
                if (isolationLevel.HasValue)
                {
                    using var ts = await _eventsContext.Database.BeginTransactionAsync(isolationLevel.Value);
                    return (await BookSeatPrivate(eventId, row, number, () => ts.CommitAsync()), null);
                }
                return (await BookSeatPrivate(eventId, row, number, () => Task.CompletedTask), null);
            }
            catch(Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private async Task<Ticket> BookSeatPrivate(long eventId, int row, int number, Func<Task> action)
        {
            var seat = await _eventsContext.Seats.FirstAsync(s =>
                s.Event.Id == eventId
                && s.Row == row
                && s.Number == number
                && s.Ticket == null);

            if (seat.Ticket == null)
            {
                seat.Ticket = new Ticket();
            }
            await Task.Delay(new Random().Next(0, 10));
            await _eventsContext.SaveChangesAsync();
            await action();
            return seat.Ticket;
        }

        public async Task<(TicketRowVersion, string)> BookSeatTransactionRowVersioned(long eventId, int row, int number)
        {
            try
            {
                var seat = await _eventsContext.SeatsRowVersion.FirstAsync(s =>
                   s.Event.Id == eventId
                   && s.Row == row
                   && s.Number == number
                   && s.Ticket == null);

                if (seat.Ticket == null)
                {
                    seat.Ticket = new TicketRowVersion();
                }
                await Task.Delay(new Random().Next(0, 10));
                await _eventsContext.SaveChangesAsync();
                return (seat.Ticket, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
    }
}
