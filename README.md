# MethodBoundaryAspect.Fody for Unity

Unity development is full of repetitive patterns: logging method calls for debugging, handling exceptions gracefully, measuring performance bottlenecks. You end up with the same `try-catch`, `Debug.Log`, and timing code scattered everywhere.

**Aspect-Oriented Programming (AOP) solves this.** Write your cross-cutting concerns once as attributes, apply them anywhere with zero boilerplate.

## Why You Need This

**Before AOP:**
```csharp
public async UniTask LoadPlayerData(string playerId)
{
    Debug.Log("[LoadPlayerData] Starting");

    try 
    {
        var data = await _api.GetPlayerData(playerId);
        Debug.Log($"[LoadPlayerData] Completed");
        return data;
    }
    catch (Exception ex)
    {
        Debug.LogError($"[LoadPlayerData] Failed: {ex.Message}");
        throw;
    }
}
```

**After AOP:**
```csharp
[LogMethod]
public async UniTask LoadPlayerData(string playerId)
{
    var data = await _api.GetPlayerData(playerId);
    return data;
}
```

Same functionality, less boilerplate. **Focus on business logic, not logging plumbing.**

### Create your own aspects:
```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
public sealed class SafeExecutionAttribute : OnMethodBoundaryAspect
{
    public override void OnException(MethodExecutionArgs args)
    {
        var methodInfo = args.Method as MethodInfo;
        var className = methodInfo?.DeclaringType?.Name ?? "Unknown";
        var methodName = methodInfo?.Name ?? "Unknown";
        var operationName = $"{className}.{methodName}";

        if (args.Exception is OperationCanceledException)
        {
            Debug.Log($"[CANCELLED] {operationName} was cancelled");
        }
        else
        {
            Debug.LogError($"[ERROR] {operationName} failed: {args.Exception.Message}");
        }
        
        // Continue execution instead of throwing
        args.FlowBehavior = FlowBehavior.Return;
    }
}
```

## Installation

### 1. Add via UPM:
```
https://github.com/finerace/com.finerace.loxodon.fody.methodboundaryaspect.git
```

### 2. Paste this to your `FodyWeavers.xml` in Assets\LoxodonFramework\Editor\AppData:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <AssemblyNames>
    <Item>Assembly-CSharp</Item>
  </AssemblyNames>
  <MethodBoundaryAspect />
</Weavers>
```

## Features

- **UniTask support** - Full async/await compatibility
- **Anonymous methods** - Works with lambdas and local functions
- **Exception handling** - Automatic try/catch wrapping
- **Method interception** - Entry/exit hooks for logging
- **Performance monitoring** - Execution time tracking via custom attributes
- **Custom attributes** - Create your own aspects

## Requirements

- Unity 2021.3+
- Mono or IL2CPP
- WARNING! Do not apply AOP attributes to classes. This will break the build process.

## Changelog

### v1.1.2
- Added anonymous methods and lambda support
- Fixed compiler-generated class handling
- Improved weaving stability

### v1.1.1
- Fixed UniTask async method errors
- Improved UniTask compatibility

### v1.1.0
- Added UniTask support
- Automatic async state machine detection
- Optimized async/await handling

### v1.0.0
- Initial Unity release
- Basic MethodBoundaryAspect functionality
- Loxodon Framework integration

## Links

- [Original MethodBoundaryAspect.Fody](https://github.com/vescon/MethodBoundaryAspect.Fody)