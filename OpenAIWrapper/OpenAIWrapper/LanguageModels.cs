namespace Zintom.OpenAIWrapper;

/// <summary>
/// List of available language models.
/// </summary>
public static class LanguageModels
{

    /// <summary>
    /// GPT-3.5
    /// </summary>
    public static string GPT_3_5_Turbo { get; } = "gpt-3.5-turbo";

    /// <summary>
    /// GPT-3.5 with function calling capability.
    /// </summary>
    public static string GPT_3_5_Turbo_0613 { get; } = "gpt-3.5-turbo-0613";

    /// <summary>
    /// GPT-4
    /// </summary>
    public static string GPT_4 { get; } = "gpt-4";

    /// <summary>
    /// GPT-4 with function calling capability.
    /// </summary>
    public static string GPT_4_0613 { get; } = "gpt-4-0613";

}
