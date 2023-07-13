## Function Calling
### Define a function:
Define a function (a standard C# method), I create an "Add" method here which adds two numbers and returns the result, optionally changing the base:

*(Note that the method returns `string`, this is because the result of a function call needs to be read by the model (as a string). This applies to all functions provided to the model. The model will intepret the return value however it wishes, whether that be int, floating-point, string, etc)*
```c#
private static string Add(int a, int b, int toBase = 10)
{
    // Code goes here...
}
```
### Decorate the method:
Decorate the method with the relevant attributes `FunctionDescription`, `ParamDescription`, and (if relevant) `EnumValues` ([more on this](/Documentation/attributes.md)); which will enable the ChatGPT class to parse the method.

<details closed>
<summary>Further reading (expand)</summary>

*The `ChatGPT` class parses the method into a `FunctionDefinition` prior to sending it to the model. A `FunctionDefinition` is a serializable object which the model recognises)*
</details>

```c#
[FunctionDescription("Adds 'b' to 'a'.")]
private static string Add([ParamDescription("The first value", true)] int a,
                          [ParamDescription("The second value", true)] int b,
                          [ParamDescription("What base the output should be in", false), EnumValues(2, 10, 16)] int toBase = 10)
{
    // Code goes here...
}
```
<details closed>
<summary>Alternative to decorating a method (expand)</summary>
    
You can create the `FunctionDefinition` manually:
```c#
FunctionDefinition additionFunction = new FunctionDefinition.Builder("Add", "Adds 'b' to 'a'.")
  .AddParameter("a", "integer", "The first value", true)
  .AddParameter("b", "integer", "The second value", true)
  .AddParameter("toBase", "What base the output should be in", false, new int[] { 2, 10, 16 })
  .SetMethod(Add)
  .Build();
```

This is functionally identical to decorating the method.
</details>

### Call the model:
Call the ChatCompletions API with your function:

*(Note: you must use the 0613 variants of both GPT 3.5 Turbo and GPT-4 as of 7th July 2023 because the default models do not currently support functions)*
```c#
var response = await gpt.GetChatCompletion(
                          messages: new List<Zintom.OpenAIWrapper.Models.Message> {
                            new() { Role = "user", Content = "What is 9 + 900?" }
                          },
                          options: new ChatGPT.ChatCompletionOptions() { Model = LanguageModels.GPT_3_5_Turbo_0613 },
                          functions: Add);
```
