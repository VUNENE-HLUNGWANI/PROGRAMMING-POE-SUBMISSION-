using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using RaceDayAPI.Filters;
using RaceDayAPI.Tests.Helpers;
using Xunit;

namespace RaceDayAPI.Tests
{
    public class SessionAuthorizeTests
    {
        private static ActionExecutingContext CreateContext(string? role, int? userId)
        {
            var http = new DefaultHttpContext { Session = new TestSession() };

            if (role != null && userId != null)
            {
                http.Session.SetString("Role", role);
                http.Session.SetInt32("UserId", userId.Value);
            }

            var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
            return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        }

        [Fact]
        public void NotLoggedIn_Returns401()
        {
            var context = CreateContext(null, null);

            new SessionAuthorizeAttribute("Organiser").OnActionExecuting(context);

            Assert.IsType<UnauthorizedObjectResult>(context.Result);
        }

        [Fact]
        public void Participant_AccessingOrganiserEndpoint_Returns403()
        {
            var context = CreateContext("Participant", 2);

            new SessionAuthorizeAttribute("Organiser").OnActionExecuting(context);

            var response = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(403, response.StatusCode);
        }

        [Fact]
        public void Organiser_AccessingParticipantEndpoint_Returns403()
        {
            var context = CreateContext("Organiser", 1);

            new SessionAuthorizeAttribute("Participant").OnActionExecuting(context);

            var response = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(403, response.StatusCode);
        }

        [Fact]
        public void Organiser_AccessingOrganiserEndpoint_IsAllowed()
        {
            var context = CreateContext("Organiser", 1);

            new SessionAuthorizeAttribute("Organiser").OnActionExecuting(context);

            Assert.Null(context.Result);
        }

        [Fact]
        public void AnyLoggedInUser_AccessingEndpointWithNoRoleRestriction_IsAllowed()
        {
            var context = CreateContext("Participant", 2);

            new SessionAuthorizeAttribute().OnActionExecuting(context);

            Assert.Null(context.Result);
        }
    }
}