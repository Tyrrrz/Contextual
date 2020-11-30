using System.Threading;
using System.Threading.Tasks;
using Contextual.Contexts;
using Xunit;

namespace Contextual.Tests
{
    public class CancellationContextSpecs
    {
        [Fact]
        public async Task Cancellation_context_can_be_used_to_implicitly_signal_cancellation()
        {
            async Task DoSomethingAsync()
            {
                var cancellation = Context.Use<CancellationContext>();
                await Task.Delay(20, cancellation.Token);
            }

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            using (Context.Provide(new CancellationContext(cts.Token)))
            {
                await Assert.ThrowsAsync<TaskCanceledException>(async () => await DoSomethingAsync());
            }
        }

        [Fact]
        public async Task Cancellation_context_does_not_signal_cancellation_if_it_is_not_provided()
        {
            async Task DoSomethingAsync()
            {
                var cancellation = Context.Use<CancellationContext>();
                await Task.Delay(20, cancellation.Token);
            }

            // No cancellation
            await DoSomethingAsync();
        }
    }
}