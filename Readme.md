# Contextual

[![Build](https://github.com/Tyrrrz/Contextual/workflows/main/badge.svg?branch=master)](https://github.com/Tyrrrz/Contextual/actions)
[![Coverage](https://codecov.io/gh/Tyrrrz/Contextual/branch/master/graph/badge.svg)](https://codecov.io/gh/Tyrrrz/Contextual)
[![Version](https://img.shields.io/nuget/v/Contextual.svg)](https://nuget.org/packages/Contextual)
[![Downloads](https://img.shields.io/nuget/dt/Contextual.svg)](https://nuget.org/packages/Contextual)
[![Discord](https://img.shields.io/discord/869237470565392384?label=discord)](https://discord.gg/2SUWKFnHSm)
[![Donate](https://img.shields.io/badge/donate-$$$-purple.svg)](https://tyrrrz.me/donate)

✅ **Project status: active**. [What does it mean?](https://github.com/Tyrrrz/.github/blob/master/docs/project-status.md)

**Contextual** is a library that helps share data between operations executing within the same logical scope.
It offers a robust and easily testable way to facilitate _implicit parameters_ in your code.

Inspired by React's [Context API](https://reactjs.org/docs/context.html), which uses a similar approach for threading data through component hierarchies.

💬 **If you want to chat, join my [Discord server](https://discord.gg/2SUWKFnHSm)**.

## Download

📦 [NuGet](https://nuget.org/packages/Contextual): `dotnet add package Contextual`

## Usage

This library allows you to establish _contexts_ which encapsulate data (or state) that can be provided and consumed by methods executing on the same callstack.
Contexts are somewhat similar to exceptions in the sense that they can move through the callstack, but instead of being thrown from below and caught from above, they are provided from above and consumed from below.

### Providing and using contexts

To define a context, create a class that inherits from the `Context` type as shown below:

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

Note that a typical context will usually have two constructors:

- Primary constructor that initializes the object with the provided value(s)
- Fallback (parameterless) constructor that initializes the object with default value(s)

The fallback constructor is required by the library as it's used to guarantee that a valid instance of the context is always available, even if it hasn't been explicitly provided.

Once defined, an instance of the context can be resolved by calling `Context.Use<MyContext>()`:

```csharp
void PrintValue()
{
    // Get the instance of the context...

    // The return is guaranteed to never be null as the parameterless constructor
    // is used to create a fallback if an instance hasn't been explicitly provided.

    var ctx = Context.Use<MyContext>();

    Console.WriteLine(ctx.Value);
}
```

To provide a specific instance of the context, call `Context.Provide(...)`:

```csharp
void Main()
{
    using (Context.Provide(new MyContext("Hello world!")))
    {
        // Custom context instance is accessible within this scope
        PrintValue(); // prints "Hello world!" to the console
    }

    // At this point, the context reverts back to the initial (default) instance
    PrintValue(); // prints "default" to the console
}
```

Calling `Context.Provide(...)` pushes a new instance of the context, which makes it available to subsequent operations.
This returns an `IDisposable` object that you must wrap in a `using` statement to designate the scope in which the context instance can be resolved.
Once the execution reaches the end of that scope, the context will get reset to the previously provided (or default) instance.

Additionally, `Context.Provide(...)` can be called multiple times to create scopes nested within each other:

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

Contexts are also persisted separately depending on their type, which allows a single operation to depend on contexts of multiple types simultaneously:

```csharp
using (Context.Provide(new FooContext("foo")))
{
    // This context is of a different type, so it's persisted separately
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

The underlying implementation in **Contextual** makes use of [`AsyncLocal`](https://docs.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1) to synchronize contexts between threads.
This means that if one async method calls another async method, they will both have access to the same contexts, even if they end up executing on separate threads:

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

### Example usages

#### Using contexts for cancellation

Contexts are generally very useful for propagating infrastructural concerns across long chain of method calls.
One such example is cancellation signals: instead of routinely passing `CancellationToken` as parameter to every method, we can simply establish a shared context.

To do that, create a context that encapsulates a cancellation token:

```csharp
class CancellationContext : Context
{
    public CancellationToken Token { get; }

    public CancellationContext(CancellationToken token) => Token = token;

    // Default -> no cancellation
    public CancellationContext() : this(CancellationToken.None) {}
}
```

And then consume it inside a cancellable operation:

```csharp
HttpClient _httpClient = new HttpClient();

async Task DoSomething()
{
    // Resolve cancellation implicitly
    // (if it hasn't been provided, we get a default value with an empty token)
    var cancellation = Context.Use<CancellationContext>();

    // Pass the cancellation token to HttpClient
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

> Note that **Contextual** already comes with an implementation of `CancellationContext` built-in, so you don't need to create your own.
> The example above is just for reference.

#### Using contexts for logging

Similarly, contexts can also be used for a logging mechanism that does not require explicitly passing `ILogger` around:

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

    // Provide a custom context to override the logger
    using (var logFile = File.CreateText("log.txt"))
    using (Context.Provide(new LogContext(logFile)))
    {
        DoSomething(); // writes logs to file instead
    }
}
```

#### Using contexts for non-deterministic inputs

Normally, non-deterministic inputs can be quite difficult to test.
For example, when retrieving current system time, a common approach is to establish some kind of `IDateTimeProvider` abstraction that has two implementations: one for production usage and a fake one that allows us to substitute the value for testing purposes.

Instead, contexts can offer a simpler alternative:

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

#### Using contexts for dependency injection

Contexts can also be used as a way to facilitate dependency injection:

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

Although resolving services as shown above may remind you of the [_service locator_](https://en.wikipedia.org/wiki/Service_locator_pattern) anti-pattern, an important difference is that the container here is isolated within a specific scope, which prevents it from being shared globally.

#### Using contexts to track recursion

Because contexts are persisted in a structure that mimics the callstack, they can be used to track recursive calls.
As an example, here's how you can use a context to prevent [_indirect recursion_](https://en.wikipedia.org/wiki/Mutual_recursion) on a specific method:

```csharp
class RecursionContext : Context
{
    public bool IsRecursing { get; }

    public RecursionContext(bool isRecursing) =>
        IsRecursing = isRecursing;

    public RecursionContext() : this(false) {}
}

void Log(string message)
{
    // Imagine this is a very complex logging method
    // that also relays calls to some other methods.

    // It's possible those other methods will in turn
    // attempt to log something as well, which will enter
    // a recursive chain that's likely going to end in
    // a stack overflow exception.

    // To prevent this, we can use a context to indicate whether
    // this method has been called recursively and make an early
    // return if so.

    var ctx = Context.Use<RecursionContext>();

    // Already logging? Return early
    if (ctx.IsRecursing)
    {
        return;
    }

    // Otherwise, provide a context for other operations
    using (Context.Provide(new RecursionContext(true)))
    {
        // Write the message to a file
        File.AppendAllText("log.txt", message);

        // And also call some other method
        DoSomethingElse();
    }
}

void DoSomethingElse()
{
    // Do stuff
    // ...

    // This message will NOT be logged if `DoSomethingElse()` is
    // called from `Log(...)` recursively.
    Log("Did stuff successfully");
}
```
