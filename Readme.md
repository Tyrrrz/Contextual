# Contextual

[![Build](https://github.com/Tyrrrz/Contextual/workflows/CI/badge.svg?branch=master)](https://github.com/Tyrrrz/Contextual/actions)
[![Coverage](https://codecov.io/gh/Tyrrrz/Contextual/branch/master/graph/badge.svg)](https://codecov.io/gh/Tyrrrz/Contextual)
[![Version](https://img.shields.io/nuget/v/Contextual.svg)](https://nuget.org/packages/Contextual)
[![Downloads](https://img.shields.io/nuget/dt/Contextual.svg)](https://nuget.org/packages/Contextual)
[![Donate](https://img.shields.io/badge/donate-$$$-purple.svg)](https://tyrrrz.me/donate)

**Project status: active**.

Contextual offers a simple foundation to safely provide and consume implicit parameters using stacked contexts and `AsyncLocal`. Inspired by React's [Context API](https://reactjs.org/docs/context.html).

## Download

- [NuGet](https://nuget.org/packages/Contextual): `dotnet add package Contextual`

## Usage

Contextual allows you to establish _contexts_ for operations nested in the callstack. You can think of it as something conceptually similar to throwing and catching exceptions, but in the opposite direction. Contexts are provided above in the callstack and consumed from below.

In essence, defining contexts allows you to provide _implicit parameters_ to your methods. This enables many different use cases.

### Providing and using contexts

To define a context, create a class that inherits from `Context`:

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

Now you can provide and consume the context inside the callstack:

```csharp
void PrintValue()
{
    var ctx = Context.Use<MyContext>();
    Console.WriteLine(ctx.Value);
}

void Main()
{
    using (Context.Provide(new MyContext("Hello world!"))
    {
        PrintValue(); // prints "Hello world!" to the console
    }

    PrintValue(); // prints "default" to the console
}
```

In order to be consumed, your context class must also provide a parameterless constructor. That constructor is invoked to create a fallback instance of the context, when none has been explicitly provided. This also means that calling `Context.Use<T>()` is guaranteed to always return a valid, non-null instance of the context.

Provided contexts are arranged on a stack, with the nearest instance being the one returned to the caller:

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

Contexts are also correctly persisted between threads inside asynchronous workflows:

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
    await Task.WhenAll(fooTask, barTask).ConfigureAwait(false);
}
```

### Using contexts for cancellation

Among other things, contexts can be useful for propagating cancellation signals. Instead of routinely passing `CancellationToken` as parameter to every nested method, we can simply establish an ambient context.

To do that, we need to create a cancellation context:

```csharp
class CancellationContext : Context
{
    public CancellationToken Token { get; }

    public CancellationContext(CancellationToken token) => Token = token;

    // Default token -> no cancellation
    public CancellationContext() : this(default) {}
}
```

And then make use of it like shown below:

```csharp
HttpClient _httpClient = new HttpClient();

async Task DoSomething()
{
    // Retrieve cancellation implicitly
    var cancellation = Context.Use<CancellationContext>();    

    // Will abort if the token is triggered
    using var request = new HttpRequestMessage(HttpMethod.Post, "...");
    await _httpClient.SendAsync(request, cancellation.Token);
}

async Task Main()
{
    // Timeout of 5 seconds
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    using (Context.Provide(new CancellationContext(cts.Token)))
    {
        // Note: cancellation token is not explicitly passed
        await DoSomething();
    }
}
```

Note: Contextual provides an implementation of `CancellationContext` out of the box, so you don't have to create it yourself.

### Using contexts for logging

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

### Using contexts for non-deterministic inputs

You can use contexts to model non-deterministic inputs, such as for example current system time:

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

### Using contexts for dependency injection

As with other types of implicit parameters, contexts can be used as a means to achieve dependency injection:

```csharp
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
