using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace osafw;

[Route("mvc/[controller]")]
public class ExtAuthController : Controller
{
    [Route("login")]
    public IActionResult Login(string provider, string redirect_uri)
    {
        var host_url = $"{Request.Scheme}://{Request.Host}";

        var full_redirect_uri = host_url + redirect_uri;

        return new ChallengeResult(
           provider,
           new AuthenticationProperties
           {
               RedirectUri = full_redirect_uri
           });
    }
}