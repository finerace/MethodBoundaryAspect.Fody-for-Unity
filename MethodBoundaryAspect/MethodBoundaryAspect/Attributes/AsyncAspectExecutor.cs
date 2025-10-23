using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MethodBoundaryAspect.Fody.Attributes;

// This helper class is injected into the user's assembly to handle async methods
// in a clean, non-IL-intrusive way using Task.ContinueWith.
public static class AsyncAspectExecutor
{
    // This method handles async methods that return Task (i.e., async void)
    public static async Task ExecuteAsync(
        MethodExecutionArgs args,
        List<OnMethodBoundaryAspect> aspects,
        Func<Task> originalMethod)
    {
        // OnEntry flow
        foreach (var aspect in aspects)
        {
            aspect.OnEntry(args);
            if (args.FlowBehavior == FlowBehavior.Return)
            {
                // Early exit requested
                CallOnExit(args, aspects.AsEnumerable().Reverse().ToList());
                return;
            }
        }

        try
        {
            await originalMethod();

            // OnExit flow for successful execution
            CallOnExit(args, aspects.AsEnumerable().Reverse().ToList());
        }
        catch (Exception ex)
        {
            // OnException flow
            args.Exception = ex;
            CallOnException(args, aspects.AsEnumerable().Reverse().ToList());
            if (args.FlowBehavior != FlowBehavior.Continue)
                throw;
        }
    }

    // This generic method handles async methods that return Task<T>
    public static async Task<T> ExecuteAsync<T>(
        MethodExecutionArgs args,
        List<OnMethodBoundaryAspect> aspects,
        Func<Task<T>> originalMethod)
    {
        // OnEntry flow
        foreach (var aspect in aspects)
        {
            aspect.OnEntry(args);
            if (args.FlowBehavior == FlowBehavior.Return)
            {
                // Early exit with a potential return value
                CallOnExit(args, aspects.AsEnumerable().Reverse().ToList());
                return (T)args.ReturnValue;
            }
        }

        try
        {
            T result = await originalMethod();
            args.ReturnValue = result;
            
            // OnExit flow for successful execution
            CallOnExit(args, aspects.AsEnumerable().Reverse().ToList());
            return (T)args.ReturnValue;
        }
        catch (Exception ex)
        {
            // OnException flow
            args.Exception = ex;
            CallOnException(args, aspects.AsEnumerable().Reverse().ToList());
            if (args.FlowBehavior == FlowBehavior.Continue)
                return (T)args.ReturnValue;
            throw;
        }
    }
    
    private static void CallOnExit(MethodExecutionArgs args, List<OnMethodBoundaryAspect> reversedAspects)
    {
        foreach (var aspect in reversedAspects)
        {
            aspect.OnExit(args);
        }
    }

    private static void CallOnException(MethodExecutionArgs args, List<OnMethodBoundaryAspect> reversedAspects)
    {
        foreach (var aspect in reversedAspects)
        {
            aspect.OnException(args);
        }
    }
}