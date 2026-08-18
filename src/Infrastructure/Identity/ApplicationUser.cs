using Microsoft.AspNetCore.Identity;

namespace POM.Identity;

/// <summary>
/// Infrastructure-internal Identity adapter over the pure domain <see cref="POM.Users.User"/>.
/// It is <b>never</b> mapped as an EF <c>DbSet</c> — the custom <see cref="UserStore"/> translates
/// between this and the domain <c>User</c> row on the clean <c>users</c> table. Because it is not a
/// mapped entity, EF (and the snake_case naming convention) never creates <c>AspNetUsers</c> or any
/// other Identity schema table (ADR-0016). UserName and PhoneNumber both carry the phone number,
/// since the phone number <i>is</i> the user's identity (no password, no email login).
/// </summary>
internal sealed class ApplicationUser : IdentityUser<Guid>
{
}
