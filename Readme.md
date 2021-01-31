# Contextual

[![Build](https://github.com/Tyrrrz/Contextual/workflows/CI/badge.svg?branch=master)](https://github.com/Tyrrrz/Contextual/actions)
[![Coverage](https://codecov.io/gh/Tyrrrz/Contextual/branch/master/graph/badge.svg)](https://codecov.io/gh/Tyrrrz/Contextual)
[![Version](https://img.shields.io/nuget/v/Contextual.svg)](https://nuget.org/packages/Contextual)
[![Downloads](https://img.shields.io/nuget/dt/Contextual.svg)](https://nuget.org/packages/Contextual)
[![Donate](https://img.shields.io/badge/donate-$$$-purple.svg)](https://tyrrrz.me/donate)

✅ **Project status: active**.

Contextual is a library that allows threading contextual information through call stacks.
It offers a robust and easily testable way to facilitate _implicit parameters_ in your code.

Inspired by React's [Context API](https://reactjs.org/docs/context.html), which uses a similar approach for threading contextual data through component hierarchies.

## Download

- [NuGet](https://nuget.org/packages/Contextual): `dotnet add package Contextual`

## Usage

This library allows you to establish _contexts_ for operations nested in the callstack.
With the help of contexts you can pass implicit parameters to methods, enabling a number of quite interesting use cases.

Contexts are somewhat similar to exceptions in the sense that they can implicitly move through the callstack, but instead of being thrown from below and caught from above, they are provided from above and consumed from below.

### Providing and using contexts

To define a custom context, create a class that inherits from the abstract `Context` class as shown below:

```csharp
// A simple context that exposes a single string value
class MyContext : Context
{
    public string Value { get; }

    public MyContext(string value) => Value = value;

    // This will be called to create a fallback instance
    // when the context has not been provided.
    public MyContext() : this("default") {}
}
```

This class has two constructors, one which initializes `Value` based on the given parameter and another parameterless constructor that sets it to `"default"`.
The latter is a requirement because, by design, at least one instance of the context must be accessible at all times, and, if it hasn't been explicitly provided, the library will use the parameterless constructor to create a default fallback instance.

Note that although you can technically define a context class that does not have a parameterless constructor, you won't actually be able to use it as `Context.Use<T>(...)` has a generic constraint that enforces the presence of the constructor in the definition.

Then, in a method that is meant to depend on this context, call `Context.Use<MyContext>()` to resolve the nearest instance on the stack:

```csharp
void PrintValue()
{
    // Get the instance of the context.
    
    // The instance is guaranteed to never be null as the parameterless
    // constructor is used to resolve a fallback if it hasn't been explicitly provided.
    
    var ctx = Context.Use<MyContext>();
    
    Console.WriteLine(ctx.Value);
    
    // (we'll see how the instance is provided in the following sample)
}
```


Finally, to provide a specific instance of the context, we can call `Context.Provide(...)` somewhere above in the stack:

```csharp
void Main()
{
    using (Context.Provide(new MyContext("Hello world!")))
    {
        PrintValue(); // prints "Hello world!" to the console
    }

    PrintValue(); // prints "default" to the console
}
```

Note that when calling `Context.Provide(...)`, we get an `IDisposable` back.
It's very important to wrap it in a `using` statement because its `Dispose()` method is responsible for popping the current instance off the stack, re-establishing the previous one in the process.

When dealing with multiple provided contexts of the same type, `Context.Use<T>()` will always resolve the instance which is nearest on the callstack.
Essentially, providing a new context is a way to temporarily override the currently available instance:

```csharp
using (Context.Provide(new MyContext("foo")))
{
    PrintValue(); // prints "foo"

    using (Context.Provide(new MyContext("bar")))
    {
        PrintValue(); // prints "bar"

        using (Context.Provide(new MyContext("baz")))
        {
            PrintValue(); // prints "baz"
        }

        PrintValue(); // prints "bar"
    }

    PrintValue(); // prints "foo"
}
```

Context are also stacked separately depending on their type, so you can easily compose them:

```csharp
using (Context.Provide(new FooContext("foo")))
{
    // This context is of a different type, so it's persisted on a separate stack
    using (Context.Provide(new BarContext(42)))
    {
        using (Context.Provide(new FooContext("baz")))
        {
            var fooCtx = Context.Use<FooContext>(); // will resolve FooContext("baz")
            var barCtx = Context.Use<BarContext>(); // will resolve BarContext(42)
        }
    }
}
```

### Sharing contexts between threads

The underlying implementation in Contextual makes use of [`AsyncLocal`](https://docs.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1) to synchronize context stacks between threads.
This means that if one async method provides a context and calls another async method (which may get executed on a different thread), the latter will resolve the instance as you would normally expect:

```csharp
async Task PrintValueAsync()
{
    await Task.Delay(10);

    var ctx = Context.Use<MyContext>();
    Console.WriteLine(ctx.Value);
}

async Task SetFooAndPrintValueAsync()
{
    using (Context.Provide(new MyContext("foo")))
    {
        await Task.Delay(10);
        await PrintValueAsync();
    }
}

async Task SetBarAndPrintValueAsync()
{
    using (Context.Provide(new MyContext("bar")))
    {
        await Task.Delay(10);
        await PrintValueAsync();
    }
}

async Task ContextualAsync()
{
    var fooTask = SetFooAndPrintValueAsync();
    var barTask = SetBarAndPrintValueAsync();

    // Prints "foo" and "bar"
    await Task.WhenAll(fooTask, barTask);
}
```

### Example: using contexts for cancellation

Contexts are generally useful for propagating infrastructural concerns across long chain of method calls.
One such example is propagating cancellation signals: instead of routinely passing `CancellationToken` as parameter to every method, we can simply establish a shared context.

To do that, we need to create a context that encapsulates a cancellation token:

> Note, Contextual already comes with an implementation of `CancellationContext` built-in. The example below is shown just for reference.

```csharp
class CancellationContext : Context
{
    public CancellationToken Token { get; }

    public CancellationContext(CancellationToken token) => Token = token;

    // Default -> no cancellation
    public CancellationContext() : this(CancellationToken.None) {}
}
```

And then make use of it as shown here:

```csharp
HttpClient _httpClient = new HttpClient();

async Task DoSomething()
{
    // Retrieve cancellation implicitly.
    // If it hasn't been provided, we simply don't cancel.
    var cancellation = Context.Use<CancellationContext>();    

    // Will abort the request if the cancellation was
    // requested from upstream.
    using var request = new HttpRequestMessage(HttpMethod.Post, "...");
    using var response = await _httpClient.SendAsync(request, cancellation.Token);
    
    // ...
}

async Task Main()
{
    // Create a cancellation token source which will time out in 5 seconds
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    // Wrap the token in a context and provide it to nested operations
    using (Context.Provide(new CancellationContext(cts.Token)))
    {
        // Note that cancellation token is not passed explicitly here
        await DoSomething();
    }
}
```

### Example: using contexts for logging

Similarly, contexts can also be used for a logging implementation which does not require passing `ILogger` around:

```csharp
class LogContext : Context
{
    private readonly TextWriter _output;

    public LogContext(TextWriter output) => _output = output;

    // By default, just write to stdout
    public LogContext() : this(Console.Out) {}

    public void Write(string message) => _output.WriteLine(message);
}
```

```csharp
void DoSomething()
{
    var log = Context.Use<LogContext>();
    log.Write("Something happened");
}

void Main()
{
    DoSomething(); // writes logs to console

    using (var logFile = File.CreateText("log.txt"))
    using (Context.Provide(new LogContext(logFile)))
    {
        DoSomething(); // writes logs to file instead
    }
}
```

### Example: using contexts for non-deterministic inputs

Normally, non-deterministic inputs can be quite difficult to test.
For example, when dealing with the current system time, a common approach is to establish some kind of `IDateTimeProvider` abstraction that has two implementations: a real one for production usage and a fake one that allows us to substitute a constant value for testing purposes.

On the other hand, contexts offer us a much simpler solution to that problem:

```csharp
class DateTimeContext : Context
{
    private readonly DateTimeOffset? _override;

    // This constructor is used in tests to override the current system time
    public DateTimeContext(DateTimeOffset override) => _override = override;

    // This constructor is used in production to get the real system time
    public DateTimeContext() {}

    public DateTimeOffset GetNow() => _override ?? DateTimeOffset.Now;
}
```

```csharp
void PrintCurrentDate()
{
    var ctx = Context.Use<DateTimeContext>();
    Console.WriteLine(ctx.GetNow());
}

// Production usage (no context provided)
void Main()
{
    PrintCurrentDate(); // prints the current date
}

// Test usage (context is provided)
void Test()
{
    using (Context.Provide(new DateTimeContext(DateTimeOffset.UnixEpoch)))
    {
        PrintCurrentDate(); // prints unix epoch
    }
}
```

### Example: using contexts for dependency injection

Analogically to the case with non-deterministic inputs, we can also utilize this approach to facilitate dependency injection:

```csharp
// This implementation uses Microsoft.Extensions.DependencyInjection container,
// but can also be implemented in many other ways.
class DependencyContainerContext : Context
{
    public IServiceProvider Services { get; }

    // This constructor is used in tests to replace registered services
    public DependencyContainerContext(IServiceProvider services) =>
        Services = services;

    // This constructor is used in production to register real services
    public DependencyContainerContext()
        : this(ConfigureServices()) {}
        
    private static IServiceProvider ConfigureServices()
    {
        var serviceCollection = new ServiceCollection();
        
        // Register real services
        serviceCollection.AddSingleton<IDependency, RealDependency>();
        
        return serviceCollection.BuildServiceProvider();
    }
}
```

And to use it:

```csharp
void DoSomething()
{
    var dep = Context
        .Use<DependencyContainerContext>()
        .Services
        .GetService<IDependency>();

    // Use the dependency
    // ...
}

// Production usage (no context provided)
void Main()
{
    // Uses real dependencies
    DoSomething();
}

// Test usage (context is provided)
void Test()
{
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSingleton<IDependency, FakeDependency>();
    var serviceProvider = serviceCollection.BuildServiceProvider();

    using (Context.Provide(new DependencyContainerContext(serviceProvider)))
    {
        // Uses fake dependencies
        DoSomething();
    }
}
```
