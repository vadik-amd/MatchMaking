namespace MatchMaking.Service.Contracts;

public record MatchCompleteMessage(string MatchId, string[] UserIds);