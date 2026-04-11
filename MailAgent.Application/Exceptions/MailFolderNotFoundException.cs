namespace MailAgent.Application.Exceptions;

public sealed class MailFolderNotFoundException(string folderPath)
  : MailAgentException($"Folder '{folderPath}' was not found under Inbox.");
