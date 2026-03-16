namespace MailAgent.Api.Models;

public record ImportMailsRequest(
  string Folder,
  int TakeCount);
