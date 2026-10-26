using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Tests.Helpers;
using Xunit;

namespace RaceDayAPI.Tests
{
    public class RegistrationsControllerTests
    {
        private const int OrganiserId = 1;
        private const int ParticipantId = 2;

        private static RegistrationsController CreateController(RaceDayDbContext db, string? role, int? userId)
        {
            var controller = new RegistrationsController(db);
            TestHelper.SetController(controller, role, userId);
            return controller;
        }

        private static async Task<(Event raceEvent, Category category)> SeedEventAsync(RaceDayDbContext db, int maxParticipants = 100)
        {
            db.Users.Add(new User
            {
                UserID = ParticipantId,
                FirstName = "Thabo",
                LastName = "Mokoena",
                Email = "runner@test.com",
                Password = "x",
                PhoneNumber = "0821234567",
                Role = "Participant"
            });

            db.Participants.Add(new Participant
            {
                ParticipantID = ParticipantId,
                DateOfBirth = new DateTime(1995, 5, 20),
                Gender = "Male",
                EmergencyContactName = "Lerato",
                EmergencyContactNumber = "0839876543"
            });

            var raceEvent = TestHelper.NewEvent(OrganiserId);
            db.Events.Add(raceEvent);
            await db.SaveChangesAsync();

            var category = TestHelper.NewCategory(raceEvent.EventID, max: maxParticipants);
            db.Categories.Add(category);
            await db.SaveChangesAsync();

            return (raceEvent, category);
        }

        private static ParticipantEvent NewEnrolment(int eventId, int categoryId, int participantId, string bib, string status)
        {
            return new ParticipantEvent
            {
                EventID = eventId,
                CategoryID = categoryId,
                ParticipantID = participantId,
                Bib = bib,
                RegistrationDate = DateTime.Now,
                RegistrationStatus = status
            };
        }

        [Fact]
        public async Task Enrol_AsParticipant_RecordsPendingEnrolmentWithBib()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, category) = await SeedEventAsync(db);
            var controller = CreateController(db, "Participant", ParticipantId);

            var result = await controller.CreateRegistration(new EnrolmentRequest { EventID = raceEvent.EventID, CategoryID = category.CategoryID });

            Assert.IsType<CreatedAtActionResult>(result);
            var saved = await db.ParticipantEvents.SingleAsync();
            Assert.Equal(ParticipantId, saved.ParticipantID);
            Assert.Equal(raceEvent.EventID, saved.EventID);
            Assert.Equal(category.CategoryID, saved.CategoryID);
            Assert.Equal("Pending", saved.RegistrationStatus);
            Assert.Equal("0001", saved.Bib);
        }

        [Fact]
        public async Task Enrol_SameEventTwice_Returns409AndRecordsOnlyOne()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, category) = await SeedEventAsync(db);
            var controller = CreateController(db, "Participant", ParticipantId);
            var request = new EnrolmentRequest { EventID = raceEvent.EventID, CategoryID = category.CategoryID };

            await controller.CreateRegistration(request);
            var second = await controller.CreateRegistration(request);

            Assert.IsType<ConflictObjectResult>(second);
            Assert.Equal(1, await db.ParticipantEvents.CountAsync());
        }

        [Fact]
        public async Task Enrol_AfterRegistrationDeadline_ReturnsBadRequest()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, category) = await SeedEventAsync(db);
            raceEvent.RegistrationDeadline = DateTime.Now.AddDays(-1);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Participant", ParticipantId);

            var result = await controller.CreateRegistration(new EnrolmentRequest { EventID = raceEvent.EventID, CategoryID = category.CategoryID });

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(db.ParticipantEvents);
        }

        [Fact]
        public async Task Enrol_CategoryBelongingToAnotherEvent_ReturnsBadRequest()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, _) = await SeedEventAsync(db);
            var otherEvent = TestHelper.NewEvent(OrganiserId, "Other Race");
            db.Events.Add(otherEvent);
            await db.SaveChangesAsync();
            var otherCategory = TestHelper.NewCategory(otherEvent.EventID, "5km Open");
            db.Categories.Add(otherCategory);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Participant", ParticipantId);

            var result = await controller.CreateRegistration(new EnrolmentRequest { EventID = raceEvent.EventID, CategoryID = otherCategory.CategoryID });

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(db.ParticipantEvents);
        }

        [Fact]
        public async Task Enrol_UnknownEvent_ReturnsBadRequest()
        {
            using var db = TestHelper.CreateDb();
            await SeedEventAsync(db);
            var controller = CreateController(db, "Participant", ParticipantId);

            var result = await controller.CreateRegistration(new EnrolmentRequest { EventID = 9999, CategoryID = 1 });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Enrol_FullCategory_Returns409()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, category) = await SeedEventAsync(db, maxParticipants: 1);
            db.ParticipantEvents.Add(NewEnrolment(raceEvent.EventID, category.CategoryID, 99, "0001", "Confirmed"));
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Participant", ParticipantId);

            var result = await controller.CreateRegistration(new EnrolmentRequest { EventID = raceEvent.EventID, CategoryID = category.CategoryID });

            Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(1, await db.ParticipantEvents.CountAsync());
        }

        [Fact]
        public async Task GetMyRegistrations_ReturnsOnlyTheLoggedInParticipantsEnrolments()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, category) = await SeedEventAsync(db);
            db.ParticipantEvents.Add(NewEnrolment(raceEvent.EventID, category.CategoryID, ParticipantId, "0001", "Pending"));
            db.ParticipantEvents.Add(NewEnrolment(raceEvent.EventID, category.CategoryID, 99, "0002", "Pending"));
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Participant", ParticipantId);

            var result = await controller.GetMyRegistrations();

            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<ParticipantEvent>>(ok.Value).ToList();
            Assert.Single(list);
            Assert.Equal(ParticipantId, list[0].ParticipantID);
        }

        [Fact]
        public async Task GetRegistrationsByEvent_OwnEvent_ReturnsItsEnrolments()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, category) = await SeedEventAsync(db);
            db.ParticipantEvents.Add(NewEnrolment(raceEvent.EventID, category.CategoryID, ParticipantId, "0001", "Pending"));
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Organiser", OrganiserId);

            var result = await controller.GetRegistrationsByEvent(raceEvent.EventID);

            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<ParticipantEvent>>(ok.Value).ToList();
            Assert.Single(list);
        }

        [Fact]
        public async Task GetRegistrationsByEvent_AnotherOrganiser_Returns403()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, _) = await SeedEventAsync(db);
            var controller = CreateController(db, "Organiser", 55);

            var result = await controller.GetRegistrationsByEvent(raceEvent.EventID);

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, response.StatusCode);
        }

        [Fact]
        public async Task UpdateStatus_OrganiserOwnEvent_ConfirmsEnrolment()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, category) = await SeedEventAsync(db);
            var enrolment = NewEnrolment(raceEvent.EventID, category.CategoryID, ParticipantId, "0001", "Pending");
            db.ParticipantEvents.Add(enrolment);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Organiser", OrganiserId);

            var result = await controller.UpdateStatus(enrolment.ParticipantEventID, new EnrolmentStatusRequest { Status = "confirmed" });

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Confirmed", (await db.ParticipantEvents.SingleAsync()).RegistrationStatus);
        }

        [Fact]
        public async Task UpdateStatus_InvalidStatus_ReturnsBadRequestAndKeepsStatus()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, category) = await SeedEventAsync(db);
            var enrolment = NewEnrolment(raceEvent.EventID, category.CategoryID, ParticipantId, "0001", "Pending");
            db.ParticipantEvents.Add(enrolment);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Organiser", OrganiserId);

            var result = await controller.UpdateStatus(enrolment.ParticipantEventID, new EnrolmentStatusRequest { Status = "Banana" });

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Pending", (await db.ParticipantEvents.SingleAsync()).RegistrationStatus);
        }

        [Fact]
        public async Task UpdateStatus_AnotherOrganisersEvent_Returns403AndKeepsStatus()
        {
            using var db = TestHelper.CreateDb();
            var (raceEvent, category) = await SeedEventAsync(db);
            var enrolment = NewEnrolment(raceEvent.EventID, category.CategoryID, ParticipantId, "0001", "Pending");
            db.ParticipantEvents.Add(enrolment);
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Organiser", 55);

            var result = await controller.UpdateStatus(enrolment.ParticipantEventID, new EnrolmentStatusRequest { Status = "Confirmed" });

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, response.StatusCode);
            Assert.Equal("Pending", (await db.ParticipantEvents.SingleAsync()).RegistrationStatus);
        }

        [Fact]
        public async Task UpdateStatus_UnknownEnrolment_ReturnsNotFound()
        {
            using var db = TestHelper.CreateDb();
            await SeedEventAsync(db);
            var controller = CreateController(db, "Organiser", OrganiserId);

            var result = await controller.UpdateStatus(9999, new EnrolmentStatusRequest { Status = "Confirmed" });

            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}