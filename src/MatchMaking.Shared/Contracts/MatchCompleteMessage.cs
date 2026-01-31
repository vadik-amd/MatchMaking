namespace MatchMaking.Shared.Contracts;

public record MatchCompleteMessage(string MatchId, string[] UserIds);