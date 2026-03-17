namespace MailAgent.Application.Digest;

internal sealed record DigestEmail(int Id, string Subject, string From, DateTime DateUtc, string BodyPreview);
