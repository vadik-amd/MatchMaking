using MatchMaking.Shared.Contracts;

namespace MatchMaking.Service.Services;

public interface IMatchMakingService
{
    Task RequestMatchAsync(string userId);
    Task<MatchInfoResponse?> GetMatchInfoAsync(string userId);
    Task SaveMatchInfoAsync(string matchId, string[] userIds); 
}