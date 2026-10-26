using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Tests.Helpers;
using Xunit;

namespace RaceDayAPI.Tests
{
    public class ResultsControllerTests
    {
        private const int OrganiserId = 1;
        private const int ParticipantId = 2;

        private static ResultsController CreateController(RaceDayDbContext db, string? role, int? userId)
        {
            var controller = new ResultsController(db);
            TestHelper.SetController(controller, role, userId);
            return controller;
        }

        private static async Task<ParticipantEvent> SeedEnrolmentAsync(RaceDayDbContext db, int participantId = ParticipantId, string bib = "0001")
        {
            var raceEvent = await db.Events.FirstOrDefaultAsync();
            if (raceEvent == null)
            {
                raceEvent = TestHelper.NewEvent(OrganiserId);
                db.Events.Add(raceEvent);
                await db.SaveChangesAsync();
            }

            var category = await db.Categories.FirstOrDefaultAsync();
            if (category == null)
            {
                category = TestHelper.NewCategory(raceEvent.EventID);
                db.Categories.Add(category);
                await db.SaveChangesAsync();
            }

            var enrolment = new ParticipantEvent
            {
                EventID = raceEvent.EventID,
                CategoryID = category.CategoryID,
                ParticipantID = participantId,
                Bib = bib,
                RegistrationDate = DateTime.Now,
                RegistrationStatus = "Confirmed"
            };
            db.ParticipantEvents.Add(enrolment);
            await db.SaveChangesAsync();

            return enrolment;
        }

        private static Result NewResult(int participantEventId, int position)
        {
            return new Result
            {
                ParticipantEventID = participantEventId,
                Time = new TimeSpan(0, 52, 30),
                Position = position,
                Status = "Finished"
            };
        }

        [Fact]
        public async Task PostResult_OrganiserOwnEvent_SavesResult()
        {
            using var db = TestHelper.CreateDb();
            var enrolment = await SeedEnrolmentAsync(db);
            var controller = CreateController(db, "Organiser", OrganiserId);

            var result = await controller.PostResult(NewResult(enrolment.ParticipantEventID, 47));

            Assert.IsType<CreatedAtActionResult>(result.Result);
            var saved = await db.Results.SingleAsync();
            Assert.Equal(47, saved.Position);
            Assert.Equal("Finished", saved.Status);
        }

        [Fact]
        public async Task PostResult_AnotherOrganiser_Returns403AndSavesNothing()
        {
            using var db = TestHelper.CreateDb();
            var enrolment = await SeedEnrolmentAsync(db);
            var controller = CreateController(db, "Organiser", 55);

            var result = await controller.PostResult(NewResult(enrolment.ParticipantEventID, 1));

            var response = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, response.StatusCode);
            Assert.Empty(db.Results);
        }

        [Fact]
        public async Task PostResult_DuplicateForSameEnrolment_Returns409()
        {
            using var db = TestHelper.CreateDb();
            var enrolment = await SeedEnrolmentAsync(db);
            var controller = CreateController(db, "Organiser", OrganiserId);

            await controller.PostResult(NewResult(enrolment.ParticipantEventID, 1));
            var second = await controller.PostResult(NewResult(enrolment.ParticipantEventID, 2));

            Assert.IsType<ConflictObjectResult>(second.Result);
            Assert.Equal(1, await db.Results.CountAsync());
        }

        [Fact]
        public async Task PostResult_UnknownEnrolment_ReturnsBadRequest()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db, "Organiser", OrganiserId);

            var result = await controller.PostResult(NewResult(9999, 1));

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Empty(db.Results);
        }

        [Fact]
        public async Task GetResultsByEvent_AnotherOrganiser_Returns403()
        {
            using var db = TestHelper.CreateDb();
            await SeedEnrolmentAsync(db);
            var eventId = (await db.Events.SingleAsync()).EventID;
            var controller = CreateController(db, "Organiser", 55);

            var result = await controller.GetResultsByEvent(eventId);

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, response.StatusCode);
        }

        [Fact]
        public async Task GetResultsByEvent_OwnEvent_ReturnsItsResults()
        {
            using var db = TestHelper.CreateDb();
            var enrolment = await SeedEnrolmentAsync(db);
            db.Results.Add(NewResult(enrolment.ParticipantEventID, 3));
            await db.SaveChangesAsync();
            var eventId = (await db.Events.SingleAsync()).EventID;
            var controller = CreateController(db, "Organiser", OrganiserId);

            var result = await controller.GetResultsByEvent(eventId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<Result>>(ok.Value).ToList();
            Assert.Single(list);
            Assert.Equal(3, list[0].Position);
        }

        [Fact]
        public async Task GetMyResults_ReturnsOnlyTheLoggedInParticipantsResults()
        {
            using var db = TestHelper.CreateDb();
            var mine = await SeedEnrolmentAsync(db, ParticipantId, "0001");
            var theirs = await SeedEnrolmentAsync(db, 99, "0002");
            db.Results.Add(NewResult(mine.ParticipantEventID, 5));
            db.Results.Add(NewResult(theirs.ParticipantEventID, 6));
            await db.SaveChangesAsync();
            var controller = CreateController(db, "Participant", ParticipantId);

            var result = await controller.GetMyResults();

            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value).Cast<object>().ToList();
            Assert.Single(list);
            Assert.Equal(5, list[0].GetType().GetProperty("Position")!.GetValue(list[0]));
        }
    }
}