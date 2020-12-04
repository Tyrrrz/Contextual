using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Contextual.Tests
{
    public class MyContext : Context, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public string Value { get; }

        public MyContext(string value) => Value = value;

        public MyContext() : this("default") {}

        public void Dispose() => IsDisposed = true;
    }

    public class MyOtherContext : Context
    {
        public int Value { get; }

        public MyOtherContext(int value) => Value = value;

        public MyOtherContext() : this(0) {}
    }

    public class MainSpecs
    {
        [Fact]
        public void Retrieving_context_that_has_not_been_provided_returns_the_default_instance()
        {
            var ctx = Context.Use<MyContext>();
            ctx.Value.Should().Be("default");
        }

        [Fact]
        public void Retrieving_context_that_has_been_provided_returns_its_instance()
        {
            using (Context.Provide(new MyContext("foo")))
            {
                var ctx = Context.Use<MyContext>();
                ctx.Value.Should().Be("foo");
            }
        }

        [Fact]
        public void Retrieving_context_that_has_been_provided_multiple_times_returns_its_nearest_instance()
        {
            using (Context.Provide(new MyContext("foo")))
            {
                var ctx1 = Context.Use<MyContext>();
                ctx1.Value.Should().Be("foo");

                using (Context.Provide(new MyContext("bar")))
                {
                    var ctx2 = Context.Use<MyContext>();
                    ctx2.Value.Should().Be("bar");

                    using (Context.Provide(new MyContext("baz")))
                    {
                        var ctx3 = Context.Use<MyContext>();
                        ctx3.Value.Should().Be("baz");
                    }
                }
            }
        }

        [Fact]
        public void Retrieving_context_returns_an_instance_of_the_requested_type()
        {
            using (Context.Provide(new MyContext("foo")))
            using (Context.Provide(new MyOtherContext(13)))
            using (Context.Provide(new MyContext("bar")))
            using (Context.Provide(new MyOtherContext(24)))
            using (Context.Provide(new MyContext("baz")))
            {
                var ctx = Context.Use<MyOtherContext>();
                ctx.Value.Should().Be(24);
            }
        }

        [Fact]
        public void Disposing_the_context_scope_renders_it_unavailable()
        {
            using (Context.Provide(new MyContext("foo")))
            {
            }

            var ctx = Context.Use<MyContext>();
            ctx.Value.Should().Be("default");
        }

        [Fact]
        public void Disposing_the_context_scope_of_a_context_that_implements_IDisposable_also_calls_its_Dispose_method()
        {
            var ctx = new MyContext("foo");
            ctx.IsDisposed.Should().BeFalse();

            using (Context.Provide(ctx))
            {
            }

            ctx.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public async Task Context_stack_is_correctly_preserved_in_asynchronous_workflows()
        {
            async Task<MyContext> GetContextAsync()
            {
                await Task.Delay(10).ConfigureAwait(false);
                return Context.Use<MyContext>();
            }

            async Task<MyContext> SetFooAndGetContextAsync()
            {
                using (Context.Provide(new MyContext("foo")))
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    return await GetContextAsync().ConfigureAwait(false);
                }
            }

            async Task<MyContext> SetBarAndGetContextAsync()
            {
                using (Context.Provide(new MyContext("bar")))
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    return await GetContextAsync().ConfigureAwait(false);
                }
            }

            var fooTask = SetFooAndGetContextAsync();
            var barTask = SetBarAndGetContextAsync();

            var ctx = await GetContextAsync().ConfigureAwait(false);
            ctx.Value.Should().Be("default");

            await Task.WhenAll(fooTask, barTask).ConfigureAwait(false);

            fooTask.Result.Value.Should().Be("foo");
            barTask.Result.Value.Should().Be("bar");
        }
    }
}