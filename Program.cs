using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.VisualStudio.Threading;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class JoinableTaskFactoryUtil {
    private static readonly System.Threading.AsyncLocal<JoinableTaskContext> _context =
        new System.Threading.AsyncLocal<JoinableTaskContext>();

    public static JoinableTaskFactory Factory =>
        _context.Value?.Factory ?? (_context.Value = new JoinableTaskContext()).Factory;
}

[MemoryDiagnoser]
public class JTFBenchmark {
    private const int Timeout = 500; // Timeout to prevent indefinite blocking.
    private const int ConcurrentTasksCount = 30; // Number of concurrent tasks.


    private async Task RunWithCustomSyncContext(Func<Task> func) {
        var prevCtx = SynchronizationContext.Current;
        try {
            var ctx = new CustomSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ctx);
            await func();
        }
        finally {
            SynchronizationContext.SetSynchronizationContext(prevCtx); // Restore the previous context.
        }
    }


    private async Task NestedTask() {
        await Task.Delay(100);
    }

    private async Task ConcurrentTask(Func<Task> taskMethod) {
        var tasks = Enumerable.Range(0, ConcurrentTasksCount).Select(_ => taskMethod()).ToArray();
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task AsyncAllTheWay() {
        await RunWithCustomSyncContext(async () => { await ConcurrentTask(async () => { await NestedTask(); }); });
    }
    
    [Benchmark]
    public async Task WithSingleJTF() {
        await RunWithCustomSyncContext(async () => {
            JoinableTaskFactoryUtil.Factory.Run(async () => {
                await ConcurrentTask(async () => { await NestedTask(); });
            });
        });
    }

    [Benchmark]
    public async Task WithJTFMultipleContextNested() {
        await RunWithCustomSyncContext(async () => {
            await ConcurrentTask(async () => {
                var context = new JoinableTaskContext();
                var joinableTask = context.Factory.RunAsync(async () => {
                    var context2 = new JoinableTaskContext();
                    await context2.Factory.RunAsync(async () => { await NestedTask(); });
                });
                joinableTask.Join();
            });
        });
    }
    
    
    [Benchmark]
    public async Task WithJTFAndFactoryUtilNested() {
        await RunWithCustomSyncContext(async () => {
            await ConcurrentTask(async () => {
                var joinableTask = JoinableTaskFactoryUtil.Factory.RunAsync(async () => {
                    await JoinableTaskFactoryUtil.Factory.RunAsync(async () => { await NestedTask(); });
                });
                joinableTask.Join();
            });
        });
    }

    [Benchmark]
    public void SyncOverAsync1() {
        RunWithCustomSyncContext(() => {
            var tasks = Enumerable.Range(0, ConcurrentTasksCount).Select(_ => Task.Run(() => {
                Task.Run(async () => { Task.Run(async () => { await NestedTask(); }).Wait(); }).Wait();
            })).ToArray();

            Task.WaitAll(tasks); // This blocks synchronously until all tasks have completed.
            return Task.CompletedTask;
        });
    }

    [Benchmark]
    public Task SyncOverAsync2() {
        return RunWithCustomSyncContext(async () => {
            var tasks = Enumerable.Range(0, ConcurrentTasksCount).Select(_ => Task.Run(async () => {
                Task taskInner = Task.Run(async () => { await NestedTask(); });
                Task.WaitAll(taskInner); 
                return Task.CompletedTask;
            })).ToArray();
            Task.WaitAll(tasks); 
        });
    }
    
    [Benchmark]
    public void WithJTFDeadlockAvoidance() {
        RunWithCustomSyncContext(() => {
            Parallel.For(0, ConcurrentTasksCount, _ => {
                // Here, the current synchronization context is already the custom one set by RunWithCustomSyncContext.
    
                // Use JoinableTaskFactory to run and synchronously wait for the async operation.
                JoinableTaskFactoryUtil.Factory.Run(async delegate {
                    await Task.Delay(10).ConfigureAwait(true); // Try to marshal back to the original context.
                });
            });
    
            return Task.CompletedTask;
        });
    }
    
    
    [Benchmark]
    public void SyncOverAsyncWithDeadlockPotentialRewritten() {
        RunWithCustomSyncContext(() => {
            Parallel.For(0, ConcurrentTasksCount, _ => {
                // Here, the current synchronization context is already the custom one set by RunWithCustomSyncContext.
    
                var outerTask = Task.Run(async () => {
                    await Task.Delay(10).ConfigureAwait(true); // Try to marshal back to the original context.
                });
    
                // This will likely deadlock as the outerTask tries to marshal back to the custom context,
                // but the custom context is blocked waiting for outerTask to complete.
                if (!outerTask.Wait(Timeout)) {
                    throw new TimeoutException("Potential deadlock detected.");
                }
            });
            return Task.CompletedTask;
        });
    }

    private class CustomSynchronizationContext : SynchronizationContext {
        public override void Post(SendOrPostCallback d, object state) {
            // Simply queue the delegate and don't execute it immediately.
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }

    class Program {
        static void Main(string[] args) {
            var summary = BenchmarkRunner.Run<JTFBenchmark>();
        }
    }
}