## Function Calling
Define a function, I create a basic "Add" function here:
```c#
private static string Add(double a, double b)
{
    return (a + b).ToString();
}
```
Create your function definition which will be passed to the model:
```c#
FunctionDefinition additionFunction = new FunctionDefinition.Builder("Add", "Adds 'b' to 'a'.")
  .AddParameter("a", "integer", "The first value", true)
  .AddParameter("b", "integer", "The second value", true)
  .AddParameter("base", "What base the output should be in", false, new int[] { 2, 10, 16 })
  .SetMethod(Add)
  .Build();
```
**Alternatively:**

*Annotate* an existing function and have the ChatGPT class generate the FunctionDefinitions behind the scenes:
```c#
[FunctionDescription("Adds 'b' to 'a'.")]
private static string Add([ParamDescription("The first value", true)] int a,
                          [ParamDescription("The second value", true)] int b,
                          [ParamDescription("What base the output should be in", false), EnumValues(typeof(int), 2, 10, 16)] int @base)
{
    // Code goes here...
}
```
Call the ChatCompletions API with your function
*(note you must use the 0613 variants of both GPT 3.5 Turbo and GPT-4 as of 7th July 2023 because the default models do not currently support functions)*:
```c#
var response = await gpt.GetChatCompletion(
                          messages: new List<Zintom.OpenAIWrapper.Models.Message> { new Zintom.OpenAIWrapper.Models.Message() { Role = "user", Content = "What is 9 + 900?" } },
                          options: new ChatGPT.ChatCompletionOptions() { Model = LanguageModels.GPT_3_5_Turbo_0613 },
                          functions: additionFunction);
```
