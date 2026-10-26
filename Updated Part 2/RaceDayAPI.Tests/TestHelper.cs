using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using RaceDayAPI.Data;
using RaceDayAPI.Models;

namespace RaceDayAPI.Tests
{
    /// <summary>
    /// In-memory stand-in for the server-side session used by the API.
    /// </summary>
    public class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public bool IsAvailable => true;
        public string Id => "test-session";
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[]? value)
        {
            bool found = _store.TryGetValue(key, out byte[]? stored);
            value = stored;
            return found;
        }
    }

    public static class TestHelper
    {
        public static RaceDayDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new RaceDayDbContext(options);
        }

        public static IConfiguration CreateConfig()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Registration:OrganiserCode", "TESTCODE" }
                })
                .Build();
        }

        /// <summary>
        /// Gives a controller a fake HTTP context. Pass null for role/userId to simulate a user who is not logged in.
        /// </summary>
        public static void SetController(ControllerBase controller, string? role, int? userId)
        {
            var http = new DefaultHttpContext { Session = new TestSession() };

            if (role != null && userId != null)
            {
                http.Session.SetString("Role", role);
                http.Session.SetInt32("UserId", userId.Value);
            }

            controller.ControllerContext = new ControllerContext { HttpContext = http };
        }

        public static Event NewEvent(int organiserId, string name = "Test Race")
        {
            return new Event
            {
                EventOrganiserID = organiserId,
                Name = name,
                Description = "A test event",
                Date = DateTime.Today.AddDays(30),
                Location = "Pretoria",
                StartTime = new TimeSpan(6, 0, 0),
                Routemap = "route",
                EventType = "Run",
                RegistrationDeadline = DateTime.Now.AddDays(7)
            };
        }

        public static Category NewCategory(int eventId, string name = "10km Open", int max = 100)
        {
            return new Category
            {
                EventID = eventId,
                CategoryName = name,
                Distance = 10,
                CategoryLevel = "Open",
                EntryFee = 150,
                MaxParticipants = max
            };
        }
    }
}