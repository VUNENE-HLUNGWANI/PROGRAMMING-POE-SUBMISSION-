using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Controllers;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Services;
using RaceDayAPI.Tests.Helpers;
using Xunit;

namespace RaceDayAPI.Tests
{
    public class AuthControllerTests
    {
        private static RegisterModel ParticipantModel(string email = "runner@test.com")
        {
            return new RegisterModel
            {
                FirstName = "Thabo",
                LastName = "Mokoena",
                Email = email,
                Password = "Password123!",
                PhoneNumber = "0821234567",
                Role = "Participant",
                DateOfBirth = new DateTime(1995, 5, 20),
                Gender = "Male",
                EmergencyContactName = "Lerato",
                EmergencyContactNumber = "0839876543"
            };
        }

        private static RegisterModel OrganiserModel(string code)
        {
            return new RegisterModel
            {
                FirstName = "Sipho",
                LastName = "Dlamini",
                Email = "organiser@test.com",
                Password = "Password123!",
                PhoneNumber = "0821112222",
                Role = "Organiser",
                OrganisationName = "Joburg Road Runners",
                OrganisationContact = "info@jrr.co.za",
                OrganiserCode = code
            };
        }

        private static AuthController CreateController(RaceDayDbContext db)
        {
            var controller = new AuthController(db, TestHelper.CreateConfig());
            TestHelper.SetController(controller, null, null);
            return controller;
        }

        [Fact]
        public async Task Register_ValidParticipant_Returns201AndCreatesParticipantProfile()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);

            var result = await controller.Register(ParticipantModel());

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, response.StatusCode);

            var user = await db.Users.SingleAsync();
            Assert.Equal("Participant", user.Role);
            Assert.True(await db.Participants.AnyAsync(p => p.ParticipantID == user.UserID));
        }

        [Fact]
        public async Task Register_StoresHashedPassword_NotPlainText()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);

            await controller.Register(ParticipantModel());

            var user = await db.Users.SingleAsync();
            Assert.NotEqual("Password123!", user.Password);
            Assert.True(PasswordHasher.IsHashed(user.Password));
            Assert.True(PasswordHasher.Verify("Password123!", user.Password));
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsBadRequest()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);

            await controller.Register(ParticipantModel("same@test.com"));
            var result = await controller.Register(ParticipantModel("same@test.com"));

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(1, await db.Users.CountAsync());
        }

        [Fact]
        public async Task Register_InvalidRole_ReturnsBadRequest()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);

            var model = ParticipantModel();
            model.Role = "Admin";

            var result = await controller.Register(model);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(db.Users);
        }

        [Fact]
        public async Task Register_ParticipantMissingEmergencyContact_ReturnsBadRequest()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);

            var model = ParticipantModel();
            model.EmergencyContactName = null;

            var result = await controller.Register(model);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(db.Users);
        }

        [Fact]
        public async Task Register_OrganiserWithWrongCode_Returns403()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);

            var result = await controller.Register(OrganiserModel("WRONG"));

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, response.StatusCode);
            Assert.Empty(db.Users);
        }

        [Fact]
        public async Task Register_OrganiserWithCorrectCode_Returns201AndCreatesOrganiserProfile()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);

            var result = await controller.Register(OrganiserModel("TESTCODE"));

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, response.StatusCode);

            var user = await db.Users.SingleAsync();
            Assert.Equal("Organiser", user.Role);
            Assert.True(await db.EventOrganisers.AnyAsync(o => o.EventOrganiserID == user.UserID));
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOkAndSetsSession()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);
            await controller.Register(ParticipantModel());

            var result = await controller.Login(new LoginModel { Email = "runner@test.com", Password = "Password123!" });

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Participant", controller.HttpContext.Session.GetString("Role"));
            Assert.NotNull(controller.HttpContext.Session.GetInt32("UserId"));
        }

        [Fact]
        public async Task Login_WrongPassword_Returns401AndNoSession()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);
            await controller.Register(ParticipantModel());

            var result = await controller.Login(new LoginModel { Email = "runner@test.com", Password = "WrongPassword" });

            Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Null(controller.HttpContext.Session.GetString("Role"));
        }

        [Fact]
        public async Task Login_UnknownEmail_Returns401()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);

            var result = await controller.Login(new LoginModel { Email = "nobody@test.com", Password = "Password123!" });

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Login_LegacyPlainTextPassword_SucceedsAndUpgradesToHash()
        {
            using var db = TestHelper.CreateDb();
            db.Users.Add(new User
            {
                FirstName = "Old",
                LastName = "User",
                Email = "old@test.com",
                Password = "plain123",
                PhoneNumber = "0820000000",
                Role = "Participant"
            });
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.Login(new LoginModel { Email = "old@test.com", Password = "plain123" });

            Assert.IsType<OkObjectResult>(result);
            var user = await db.Users.SingleAsync();
            Assert.True(PasswordHasher.IsHashed(user.Password));
        }

        [Fact]
        public async Task Logout_ClearsSession()
        {
            using var db = TestHelper.CreateDb();
            var controller = CreateController(db);
            await controller.Register(ParticipantModel());
            await controller.Login(new LoginModel { Email = "runner@test.com", Password = "Password123!" });

            var result = controller.Logout();

            Assert.IsType<OkObjectResult>(result);
            Assert.Null(controller.HttpContext.Session.GetString("Role"));
            Assert.Null(controller.HttpContext.Session.GetInt32("UserId"));
        }
    }
}