using MatchMaking.Service.Contracts;
using MatchMaking.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace MatchMaking.Service.Controllers;

[ApiController]
[Route("api/[controller]")] 
public class MatchMakingController(
    IMatchMakingService matchMakingService,
    ILogger<MatchMakingController> logger)
    : ControllerBase
{
    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchMatch([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Match search failed: userId is empty");
            return BadRequest(new { error = "userId is required" });
        }

        try
        {
            await matchMakingService.RequestMatchAsync(userId);
            logger.LogInformation("Match search request created for user {UserId}", userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Match search failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during match search for user {UserId}", userId);
            return BadRequest(new { error = "Failed to process match request" });
        }
    }

    [HttpGet("match")]
    [ProducesResponseType(typeof(MatchInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMatchInfo([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Get match info failed: userId is empty");
            return BadRequest(new { error = "userId is required" });
        }

        try
        {
            var matchInfo = await matchMakingService.GetMatchInfoAsync(userId);
            
            if (matchInfo == null)
            {
                logger.LogInformation("Match not found for user {UserId}", userId);
                return NotFound(new { error = "Match not found for this user" });
            }

            return Ok(matchInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving match info for user {UserId}", userId);
            return BadRequest(new { error = "Failed to retrieve match information" });
        }
    }
}