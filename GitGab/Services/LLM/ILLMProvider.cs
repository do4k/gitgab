using GitGab.Models.LLM;

namespace GitGab.Services.LLM;

public interface ILLMProvider
{
    string Name { get; }
    Task<PromptResponse> GenerateAsync(PromptRequest request, CancellationToken ct = default);
}
