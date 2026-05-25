using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rxnxt.Services.Implementations;
using Rxnxt.Web.ViewModels;
using System.Security.Claims;

namespace Rxnxt.Web.Controllers;

[AllowAnonymous]
public class AuthController : Controller
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect(returnUrl ?? "/");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _authService.ValidateUserAsync(model.UserName, model.Password);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.UserName), "Invalid Username or Password.");
            return View(model);
        }

        // Generate JWT and store in secure cookie
        var jwt = _authService.GenerateJwt(user);
        Response.Cookies.Append("RxNxtAuth", jwt, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = model.RememberMe
                ? DateTime.UtcNow.AddDays(30)
                : DateTime.UtcNow.AddMinutes(180)
        });

        // Create ClaimsPrincipal for ASP.NET Cookie Authentication
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID ?? ""),
            new Claim(ClaimTypes.Name, user.UserName ?? ""),
            new Claim(ClaimTypes.GivenName, user.FirstName ?? ""),
            new Claim(ClaimTypes.Role, user.UserGroup ?? ""),
            new Claim("TenantID", user.TenantId ?? ""),
            new Claim("FullName", $"{user.FirstName} {user.LastName}".Trim())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe
                ? DateTime.UtcNow.AddDays(30)
                : DateTime.UtcNow.AddMinutes(180)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        var returnUrl = model.ReturnUrl;
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return LocalRedirect("/");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        Response.Cookies.Delete("RxNxtAuth");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/Auth/Login");
    }
}
