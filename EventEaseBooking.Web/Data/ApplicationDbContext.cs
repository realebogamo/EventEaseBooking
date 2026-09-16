using EventEaseBooking.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace EventEaseBooking.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("Venue", t => t.HasCheckConstraint("CK_Venue_Capacity", "[Capacity] > 0"));
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Event", t => t.HasCheckConstraint("CK_Event_DateRange", "[EventEndDate] >= [EventStartDate]"));
            entity.Property(e => e.EventStartDate).HasColumnType("date");
            entity.Property(e => e.EventEndDate).HasColumnType("date");
            entity.HasIndex(e => new { e.EventStartDate, e.EventEndDate });
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Booking");
            entity.Property(b => b.BookingDate).HasDefaultValueSql("SYSUTCDATETIME()");

            // Restrict (the default): SQL Server refuses to delete a Venue/Event that
            // still has a Booking row, regardless of what the app layer checks first.
            entity.HasOne(b => b.Event)
                  .WithMany(e => e.Bookings)
                  .HasForeignKey(b => b.EventId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Venue)
                  .WithMany(v => v.Bookings)
                  .HasForeignKey(b => b.VenueId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(b => b.VenueId);
            entity.HasIndex(b => b.EventId);
        });

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Venue>().HasData(
            new Venue { VenueId = 1, VenueName = "Riverside Conference Hall", Location = "12 River Road, Johannesburg", Capacity = 250, ImageUrl = "https://placehold.co/600x400?text=Riverside+Hall" },
            new Venue { VenueId = 2, VenueName = "Oakwood Function Centre", Location = "45 Oak Street, Pretoria", Capacity = 120, ImageUrl = "https://placehold.co/600x400?text=Oakwood+Centre" },
            new Venue { VenueId = 3, VenueName = "Harbour View Ballroom", Location = "8 Harbour Drive, Cape Town", Capacity = 400, ImageUrl = "https://placehold.co/600x400?text=Harbour+View" },
            new Venue { VenueId = 4, VenueName = "Summit Boardroom", Location = "3 Summit Ave, Sandton", Capacity = 30, ImageUrl = "https://placehold.co/600x400?text=Summit+Boardroom" }
        );

        modelBuilder.Entity<Event>().HasData(
            new Event { EventId = 1, EventName = "Tech Innovators Summit", EventStartDate = new DateTime(2026, 11, 10), EventEndDate = new DateTime(2026, 11, 11), Description = "Two-day conference on emerging tech." },
            new Event { EventId = 2, EventName = "Smith-Naidoo Wedding", EventStartDate = new DateTime(2026, 10, 3), EventEndDate = new DateTime(2026, 10, 3), Description = "Wedding reception, evening event." },
            new Event { EventId = 3, EventName = "Acoustic Nights Concert", EventStartDate = new DateTime(2026, 12, 5), EventEndDate = new DateTime(2026, 12, 5), Description = "Live acoustic music evening — awaiting venue assignment." },
            new Event { EventId = 4, EventName = "Regional Sales Kickoff", EventStartDate = new DateTime(2026, 9, 28), EventEndDate = new DateTime(2026, 9, 28), Description = "Loaded before a venue was available." }
        );

        // Fixed timestamps (not DateTime.UtcNow) so the migration snapshot stays deterministic.
        modelBuilder.Entity<Booking>().HasData(
            new Booking { BookingId = 1, EventId = 1, VenueId = 1, BookingDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Booking { BookingId = 2, EventId = 2, VenueId = 3, BookingDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
