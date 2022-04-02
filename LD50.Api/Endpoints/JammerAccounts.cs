namespace LD50.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Orleans;

[ApiController]
[Route("jammer-accounts")]
public class JammerAccountsController : ControllerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly Random _random;

    public JammerAccountsController(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
        _random = new Random();
    }

    [HttpGet("{ldjamUserName}")]
    public async Task<ActionResult> Get(string? ldjamUserName)
    {
        if (string.IsNullOrWhiteSpace(ldjamUserName))
        {
            return BadRequest();
        }

        var jammerAccount = _clusterClient.GetGrain<IJammerAccount>(ldjamUserName);
        
        var isVerified = await jammerAccount.GetIsVerified();
        if (!isVerified)
        {
            var verify = _clusterClient.GetGrain<IVerifyJammerAccount>(ldjamUserName);
            var passcode = _random.Next(100_000, 1_000_000);
            var result = await verify.Init(passcode);
            if (result is NotEligible)
            {
                return NotFound();
            }
            
            var code = (result as AwaitingVerification)!.Code;
            return Ok(new {
                LdjamUserName = ldjamUserName,
                IsVerified = false,
                Authenticated = false,
                VerificationCode = code.Value,
                Passcode = code.Passcode == passcode ? code.Passcode.ToString() : "******"
            });
        }

        var password = HttpContext.Request.Headers["X-LDJAM"].ToString();
        var matches = await jammerAccount.PasswordMatches(password);
        if (!matches)
        {
            return Ok(new
            {
                LdjamUserName = ldjamUserName,
                IsVerified = true,
                Authenticated = false,
            });
        }
        else
        {
            return Ok(new
            {
                LdjamUserName = ldjamUserName,
                IsVerified = true,
                Authenticated = true,
            });
        }
    }

    [HttpPost("{ldjamUserName}/verify")]
    public async Task<ActionResult> Verify(string? ldjamUserName, [FromBody] VerificationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(ldjamUserName) ||
            string.IsNullOrWhiteSpace(dto?.Password) ||
            dto?.Passcode == null ||
            dto.Passcode < 100_000 ||
            dto.Passcode > 999_999)
        {
            return BadRequest();
        }

        var jammerAccount = _clusterClient.GetGrain<IJammerAccount>(ldjamUserName);
        var isVerified = await jammerAccount.GetIsVerified();
        if (isVerified)
        {
            return NotFound();
        }

        var verify = _clusterClient.GetGrain<IVerifyJammerAccount>(ldjamUserName);
        var result = await verify.Verify(dto.Passcode, dto.Password);
        if (result is Unverified)
        {
            return UnprocessableEntity();
        }

        return Redirect($"/jammer-accounts/{ldjamUserName}");
    }
}

public record VerificationRequestDto(
    int Passcode,
    string Password
);

public record AuthenticateRequestDto(
    string Password
);