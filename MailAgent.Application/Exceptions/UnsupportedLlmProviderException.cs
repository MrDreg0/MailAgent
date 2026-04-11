namespace MailAgent.Application.Exceptions;

public sealed class UnsupportedLlmProviderException(string provider)
  : MailAgentConfigurationException($"Unsupported LLM provider '{provider}'.");
