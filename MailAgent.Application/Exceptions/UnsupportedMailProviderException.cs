namespace MailAgent.Application.Exceptions;

public sealed class UnsupportedMailProviderException(string provider)
  : MailAgentConfigurationException($"Unsupported mail provider '{provider}'.");
