namespace MailAgent.Application.Exceptions;

public abstract class MailAgentException(string message)
  : Exception(message);
