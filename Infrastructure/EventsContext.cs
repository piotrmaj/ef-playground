using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure
{
    public class EventsContext : DbContext
    {
        public DbSet<Event> Events { get; set; }
        public DbSet<Seat> Seats { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<EventRowVersion> EventsRowVersion { get; set; }
        public DbSet<SeatRowVersion> SeatsRowVersion { get; set; }
        public DbSet<TicketRowVersion> TicketsRowVersion { get; set; }

        public EventsContext() { }

        public EventsContext(DbContextOptions<EventsContext> options) : base(options)
        {
        }

        public EventsContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (!options.IsConfigured)
            {
                options.UseSqlServer();
            }
        } 
    }

    public class Event
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public List<Seat> Seats { get; set; }
    }

    public class Seat: ISeat
    {
        public long Id { get; set; }
        public int Row { get; set; }
        public int Number { get; set; }
        public Event Event { get; set; }
        public Ticket Ticket { get; set; }
        public long? TicketId { get; set; }
    }

    public interface ISeat
    {
        public int Row { get; set; }
        public int Number { get; set; }
    }

    public class Ticket
    {
        public long Id { get; set; }
        public Seat Seat { get; set; }
    }

    public abstract class RowVersioned
    {
        [Timestamp]
        public byte[] Version { get; set; }
    }

    public class EventRowVersion: RowVersioned
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public List<SeatRowVersion> Seats { get; set; }
    }

    public class SeatRowVersion : RowVersioned, ISeat
    {
        public long Id { get; set; }
        public int Row { get; set; }
        public int Number { get; set; }
        public EventRowVersion Event { get; set; }
        public TicketRowVersion Ticket { get; set; }
        public long? TicketId { get; set; }
    }

    public class TicketRowVersion : RowVersioned
    {
        public long Id { get; set; }
        public SeatRowVersion Seat { get; set; }
    }
}
