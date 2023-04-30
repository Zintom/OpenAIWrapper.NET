using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Zintom.OpenAIWrapper.Models;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// Represents a models response for a given chat conversation.
/// </summary>
public sealed class ChatCompletion
{
    /// <summary>
    /// The ID of this completion.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    /// <summary>
    /// The unix time when this completion was created.
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; set; }

    /// <summary>
    /// The model used to generate this completion.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("usage")]
    public Usage? Usage { get; set; }

    [JsonPropertyName("choices")]
    public List<Choice>? Choices { get; set; }
}

public sealed class Usage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public sealed class Choice
{
    /// <summary>
    /// The message in this response.
    /// </summary>
    [JsonPropertyName("message")]
    public Message? Message { get; set; }

    /// <summary>
    /// The message in this response (when streaming), see <see cref="ChatGPT.GetStreamingChatCompletion(Message[], System.Action{ChatCompletion?}, ChatGPT.ChatCompletionOptions?)">GetStreamingChatCompletion</see>.
    /// </summary>
    [JsonPropertyName("delta")]
    public Message? Delta { get; set; }

    /// <summary>
    /// One of: '<i>stop</i>', '<i>length</i>', '<i>content_filter</i>', or '<i>null</i>'.
    /// <para/>
    /// <i>stop</i>: API returned complete model output.
    /// <para/>
    /// <i>length</i>: Incomplete model output due to max_tokens parameter or token limit.
    /// <para/>
    /// <i>content_filter</i>: Omitted content due to a flag from the content filters.
    /// <para/>
    /// <i>null</i>: API response still in progress or incomplete.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    /// <summary>
    /// The index of this response in the array of choices.
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }
}

/// <summary>
/// A message as a part of a conversation.
/// </summary>
public sealed class Message
{
    /// <summary>
    /// The role of the author of this message. One of '<i>system</i>', '<i>user</i>', or '<i>assistant</i>'.
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// The contents of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member