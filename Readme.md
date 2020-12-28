# Contextual

[![Build](https://github.com/Tyrrrz/Contextual/workflows/CI/badge.svg?branch=master)](https://github.com/Tyrrrz/Contextual/actions)
[![Coverage](https://codecov.io/gh/Tyrrrz/Contextual/branch/master/graph/badge.svg)](https://codecov.io/gh/Tyrrrz/Contextual)
[![Version](https://img.shields.io/nuget/v/Contextual.svg)](https://nuget.org/packages/Contextual)
[![Downloads](https://img.shields.io/nuget/dt/Contextual.svg)](https://nuget.org/packages/Contextual)
[![Donate](https://img.shields.io/badge/donate-$$$-purple.svg)](https://tyrrrz.me/donate)

✅ **Project status: active**.

Contextual offers a simple foundation to provide and consume implicit parameters using stacked contexts. Inspired by React's [Context API](https://reactjs.org/docs/context.html).

## Download

- [NuGet](https://nuget.org/packages/Contextual): `dotnet add package Contextual`

## Usage

Contextual allows you to establish _contexts_ for operations nested in the callstack. Contexts are somewhat similar to exceptions, but instead of being thrown below in the callstack and caught by callers above, they are provided above in the callstack and consumed from below. Using contexts, you can essentially pass implicit parameters, which enables a number of interesting use cases.

### Providing and using contexts

To define a context, just create a class that inherits from `Context`:

```csharp
class MyContext : Context
{
    public string Value { get; }

    public MyContext(string value) => Value = value;

    // This will be called to create a fallback instance
    // when the context has not been provided.
    public MyContext() : this("default") {}
}
```

Then, to consume the nearest instance of `MyContext`, call `Context.Use<MyContext>()`:

```csharp
void PrintValue()
{
    var ctx = Context.Use<MyContext>();
    Console.WriteLine(ctx.Value);
}
```

Finally, to provide a specific instance of the context, call `Context.Provide(...)`:

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

Note, that in order to be consumed, your context class must also provide a parameterless constructor, which is a rule enforced by the generic constraints. That constructor is invoked to create a fallback instance of the context, when none has been explicitly provided. This, in turn, means that calling `Context.Use<T>()` is always guaranteed to return a valid, non-null instance of the context, even if it hasn't been provided.

Provided contexts are grouped by type and arranged in a stack. When you call `Context.Use<T>()`, you get the nearest available instance:

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

It's important to wrap the context scope in a `using` statement, as the `Dispose()` method is responsible for poping the context off the stack. Additionally, if the context itself implements `IDisposable`, it will be called as well, so you don't have to do it separately.

The underlying implementation uses `AsyncLocal` to synchronize stacks between threads, so contexts should be correctly persisted in asynchronous workflows:

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

Among other things, contexts can be useful for propagating cancellation signals. Instead of routinely passing `CancellationToken` as parameter to every nested method, we can simply establish an ambient context.

To do that, we can create a simple cancellation context:

```csharp
class CancellationContext : Context
{
    public CancellationToken Token { get; }

    public CancellationContext(CancellationToken token) => Token = token;

    // Default token -> no cancellation
    public CancellationContext() : this(default) {}
}
```

> Note, Contextual already comes with an implementation of `CancellationContext` built-in. The above is just for reference.

And then make use of it as shown below:

```csharp
HttpClient _httpClient = new HttpClient();

async Task DoSomething()
{
    // Retrieve cancellation implicitly
    var cancellation = Context.Use<CancellationContext>();    

    // Will abort the request if the token is triggered
    using var request = new HttpRequestMessage(HttpMethod.Post, "...");
    using var response = await _httpClient.SendAsync(request, cancellation.Token);
    
    // ...
}

async Task Main()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    using (Context.Provide(new CancellationContext(cts.Token)))
    {
        // Note: cancellation token is not explicitly passed
        await DoSomething();
    }
}
```

### Example: using contexts for logging

Similarly, contexts can also be used for abstract logging:

```csharp
class LogContext : Context
{
    private readonly TextWriter _output;

    public LogContext(TextWriter output) => _output = output;

    // By default, write to stdout
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
    DoSomething(); // writes to console

    using (var logFile = File.CreateText("log.txt"))
    using (Context.Provide(new LogContext(logFile)))
    {
        DoSomething(); // writes to file
    }
}
```

### Example: using contexts for non-deterministic inputs

You can use contexts to model non-deterministic inputs, such as, for example, current system time:

```csharp
class DateTimeContext : Context
{
    private readonly DateTimeOffset? _override;

    public DateTimeContext(DateTimeOffset override) => _override = override;

    // By default, uses actual system clock
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

void Main()
{
    PrintCurrentDate(); // prints actual current date

    using (Context.Provide(new DateTimeContext(DateTimeOffset.UnixEpoch)))
    {
        PrintCurrentDate(); // prints unix epoch
    }
}
```

### Example: using contexts for dependency injection

Similarly, contexts can be used as a way to facilitate dependency injection:

```csharp
// Uses Microsoft.Extensions.DependencyInjection container, but can also
// be implemented in any other way.
class DependencyContainerContext : Context
{
    public IServiceProvider Services { get; }

    public DependencyContainerContext(IServiceProvider services) =>
        Services = services;

    public DependencyContainerContext()
        : this(new ServiceCollection().BuildServiceProvider()) {}
}
```

```csharp
void DoSomething()
{
    var dep = Context.Use<DependencyContainerContext>().Services.GetService<IDependency>();
    // ...
}

void Production()
{
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSingleton<IDependency, RealDependency>();

    using (Context.Provide(new DependencyContainerContext(serviceCollection.BuildServiceProvider())))
    {
        // Uses real dependencies
        DoSomething();
    }
}

void Test()
{
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSingleton<IDependency, FakeDependency>();

    using (Context.Provide(new DependencyContainerContext(serviceCollection.BuildServiceProvider())))
    {
        // Uses fake dependencies
        DoSomething();
    }
}
```
