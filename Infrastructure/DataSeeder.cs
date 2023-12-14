namespace Infrastructure
{
    public class DataSeeder
    {
        public static void Seed(EventsContext db)
        {
            var game = new Event
            {
                Name = "Legia vs Lech",
                Seats = new List<Seat>()
            };
            for (int i = 0; i < 10; i++)
            {
                game.Seats.AddRange(GenerateRow<Seat>(i, 15));
            }
            db.Events.Add(game);

            var gameVersioned = new EventRowVersion
            {
                Name = "Legia vs Lech",
                Seats = new List<SeatRowVersion>()
            };
            for (int i = 0; i < 10; i++)
            {
                gameVersioned.Seats.AddRange(GenerateRow<SeatRowVersion>(i, 15));
            }
            db.EventsRowVersion.Add(gameVersioned);


            db.SaveChanges();
        }

        public static List<T> GenerateRow<T>(int rowNr, int seats) where T: ISeat, new()
        {
            List<T> row = new();
            for (int i = 0; i < seats; i++)
            {
                row.Add(new T
                {
                    Number = i,
                    Row = rowNr
                });
            }
            return row;
        }
    }
}
