using ImageGen.Avalonia.Services.Secrets;

var alias = args.Length > 0 ? args[0] : "default";
await SecretServiceProbe.RunAsync(alias, account: "openrouter-probe", secret: "sk-probe-123");
