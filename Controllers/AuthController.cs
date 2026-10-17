using EntityCatalog.Dtos;
using EntityCatalog.Services;
using Microsoft.AspNetCore.Mvc;

namespace EntityCatalog.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        return result is null ? Unauthorized() : Ok(result);
    }
}
