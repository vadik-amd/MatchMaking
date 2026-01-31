using MatchMaking.Service.Responses;

namespace MatchMaking.Service.Services;

public interface IMatchMakingService
{
    Task RequestMatchAsync(string userId);
    Task<MatchInfoResponse?> GetMatchInfoAsync(string userId);
}