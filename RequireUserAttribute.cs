using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UserManagementApp.Data;

namespace UserManagementApp.Attributes
{
    public class RequireUserAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            var http = ctx.HttpContext;

            
            if (!http.Request.Cookies.TryGetValue("uid", out var uidStr) || !int.TryParse(uidStr, out var uid))
            {
                ctx.Result = new UnauthorizedResult();
                return;
            }

            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(uid);

            if (user == null || user.Status == "blocked")
            {
                ctx.Result = new UnauthorizedResult();
                return;
            }

            await next();
        }
    }
}
