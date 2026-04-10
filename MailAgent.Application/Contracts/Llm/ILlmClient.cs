using MailAgent.Application.Llm;
namespace MailAgent.Application.Contracts.Llm;

public interface ILlmClient
{
  Task<LlmGenerateResponse> Generate(LlmGenerateRequest request, CancellationToken cancellationToken);
}
