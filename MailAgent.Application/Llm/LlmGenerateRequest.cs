namespace MailAgent.Application.Llm;

public sealed record LlmGenerateRequest(
  string Model,
  string Prompt);
