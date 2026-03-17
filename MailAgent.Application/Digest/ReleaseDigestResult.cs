namespace MailAgent.Application.Digest;

public sealed record ReleaseDigestResult(int TotalFetched, int Selected, string Digest);
