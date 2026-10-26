using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Services;
using RaceDayAPI.Tests.Helpers;
using Xunit;


namespace RaceDayAPI.Tests
{
    public class EventsControllerTests
    {
        private static EventsController CreateController(RaceDayDbContext db, string? role, int? userId)
        {
            var controller = new EventsController(db);
            TestHelper.SetController(controller, role, userId);
            return controller;
        }

        [Fact]
        public async Task CreateEvent_AsOrganiser_SavesEventLinkedToOrganiser()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db, "Organiser", 7);

            var result = await controller.CreateEvent(TestHelper.NewEvent(organiserId: 999));

            Assert.IsType<CreatedAtActionResult>(result);
            var saved = await db.Events.SingleAsync();
            Assert.Equal(7, saved.EventOrganiserID);
        }

        [Fact]
        public async Task CreateEvent_AsParticipant_Returns403AndSavesNothing()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db, "Participant", 2);

            var result = await controller.CreateEvent(TestHelper.NewEvent(organiserId: 2));

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, response.StatusCode);
            Assert.Empty(db.Events);
        }

        [Fact]
        public async Task CreateEvent_NotLoggedIn_Returns403AndSavesNothing()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db, null, null);

            var result = await controller.CreateEvent(TestHelper.NewEvent(organiserId: 1));

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, response.StatusCode);
            Assert.Empty(db.Events);
        }

        [Fact]
        public async Task UpdateEvent_OwnEvent_UpdatesFields()
        {
            using var db = TestHelper.CreateDb();
            var existing = TestHelper.NewEvent(organiserId: 1, name: "Old Name");
            db.Events.Add(existing);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Organiser", 1);

            var update = TestHelper.NewEvent(organiserId: 1, name: "New Name");
            update.Location = "Cape Town";
            var result = await controller.UpdateEvent(existing.EventID, update);

            Assert.IsType<OkObjectResult>(result);
            var saved = await db.Events.SingleAsync();
            Assert.Equal("New Name", saved.Name);
            Assert.Equal("Cape Town", saved.Location);
        }

        [Fact]
        public async Task UpdateEvent_AnotherOrganisersEvent_Returns403AndKeepsOriginal()
        {
            using var db = TestHelper.CreateDb();
            var existing = TestHelper.NewEvent(organiserId: 1, name: "Original");
            db.Events.Add(existing);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Organiser", 2);

            var result = await controller.UpdateEvent(existing.EventID, TestHelper.NewEvent(organiserId: 2, name: "Hijacked"));

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, response.StatusCode);
            Assert.Equal("Original", (await db.Events.SingleAsync()).Name);
        }

        [Fact]
        public async Task UpdateEvent_UnknownId_ReturnsNotFound()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db, "Organiser", 1);

            var result = await controller.UpdateEvent(999, TestHelper.NewEvent(organiserId: 1));

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteEvent_OwnEvent_RemovesIt()
        {
            using var db = TestHelper.CreateDb();
            var existing = TestHelper.NewEvent(organiserId: 1);
            db.Events.Add(existing);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Organiser", 1);

            var result = await controller.DeleteEvent(existing.EventID);

            Assert.IsType<OkObjectResult>(result);
            Assert.Empty(db.Events);
        }

        [Fact]
        public async Task DeleteEvent_AsParticipant_Returns403AndKeepsEvent()
        {
            using var db = TestHelper.CreateDb();
            var existing = TestHelper.NewEvent(organiserId: 1);
            db.Events.Add(existing);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Participant", 2);

            var result = await controller.DeleteEvent(existing.EventID);

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, response.StatusCode);
            Assert.Single(db.Events);
        }

        [Fact]
        public async Task DeleteEvent_AnotherOrganisersEvent_Returns403()
        {
            using var db = TestHelper.CreateDb();
            var existing = TestHelper.NewEvent(organiserId: 1);
            db.Events.Add(existing);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Organiser", 2);

            var result = await controller.DeleteEvent(existing.EventID);

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, response.StatusCode);
            Assert.Single(db.Events);
        }

        [Fact]
        public async Task GetMyEvents_ReturnsOnlyTheLoggedInOrganisersEvents()
        {
            using var db = TestHelper.CreateDb();
            db.Events.Add(TestHelper.NewEvent(organiserId: 1, name: "Mine"));
            db.Events.Add(TestHelper.NewEvent(organiserId: 2, name: "Theirs"));
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Organiser", 1);

            var result = await controller.GetMyEvents();

            var events = result.Value!.ToList();
            Assert.Single(events);
            Assert.Equal("Mine", events[0].Name);
        }

        [Fact]
        public async Task GetEvent_UnknownId_ReturnsNotFound()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db, "Participant", 2);

            var result = await controller.GetEvent(12345);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }
    }
}