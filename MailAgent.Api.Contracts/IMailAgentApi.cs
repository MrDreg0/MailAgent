using MailAgent.Api.Contracts.DailyDigests;
using Refit;

namespace MailAgent.Api.Contracts;

public interface IMailAgentApi
{
  [Post("/daily-digests/{digestDate}/regenerate")]
  Task<DailyDigestDocumentResponse> RegenerateDailyDigest(
    string digestDate,
    [Query] string folder,
    CancellationToken cancellationToken = default);
}
