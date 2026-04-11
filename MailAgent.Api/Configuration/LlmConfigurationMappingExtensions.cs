using System.Globalization;
using MailAgent.Application.Llm;

namespace MailAgent.Api.Configuration;

internal static class LlmConfigurationMappingExtensions
{
  internal static LlmSettings ToRuntimeSettings(this LlmConfiguration configuration)
  {
    return new LlmSettings
    {
      Provider = configuration.Provider!.Value,
      BaseUrl = configuration.BaseUrl!.Trim(),
      Timeout = TimeSpan.FromMinutes(int.Parse(configuration.TimeoutMinutes!, CultureInfo.InvariantCulture)),
      FastModel = configuration.FastModel!.Trim(),
      MainModel = configuration.MainModel!.Trim(),
    };
  }
}
