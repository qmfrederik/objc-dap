using Objc.Dap;

namespace ObjcDap.Tests
{
    public class VariableReferenceStoreTests
    {
        [Fact]
        public void Add_ReturnsUniqueReferencesAboveLldbDapRange()
        {
            var store = new VariableReferenceStore();

            var first = store.Add(0x1000);
            var second = store.Add(0x2000);

            Assert.NotEqual(first, second);
            Assert.True(first >= VariableReferenceStore.FirstReference);
            Assert.True(second >= VariableReferenceStore.FirstReference);
            Assert.Equal(2, store.Count);
        }

        [Fact]
        public void TryGetAddress_ReturnsStoredAddress()
        {
            var store = new VariableReferenceStore();
            var reference = store.Add(0x1234);

            Assert.True(store.TryGetAddress(reference, out var address));
            Assert.Equal((nuint)0x1234, address);
        }

        [Fact]
        public void TryGetAddress_UnknownReference_ReturnsFalse()
        {
            var store = new VariableReferenceStore();

            Assert.False(store.TryGetAddress(1, out _));
            Assert.False(store.TryGetAddress(VariableReferenceStore.FirstReference, out _));
        }

        [Fact]
        public void Clear_RemovesAllReferences()
        {
            var store = new VariableReferenceStore();
            var first = store.Add(0x1000);
            var second = store.Add(0x2000);

            store.Clear();

            Assert.Equal(0, store.Count);
            Assert.False(store.TryGetAddress(first, out _));
            Assert.False(store.TryGetAddress(second, out _));
        }

        [Fact]
        public void Clear_DoesNotReuseReferences()
        {
            var store = new VariableReferenceStore();
            var stale = store.Add(0x1000);

            store.Clear();
            var fresh = store.Add(0x2000);

            Assert.NotEqual(stale, fresh);
            Assert.False(store.TryGetAddress(stale, out _));
            Assert.True(store.TryGetAddress(fresh, out var address));
            Assert.Equal((nuint)0x2000, address);
        }

        [Fact]
        public async Task ConcurrentAddAndClear_DoesNotThrow()
        {
            var store = new VariableReferenceStore();
            var cancellationToken = TestContext.Current.CancellationToken;

            var adders = Enumerable.Range(0, 4).Select(worker => Task.Run(
                () =>
                {
                    for (int i = 0; i < 10_000; i++)
                    {
                        var reference = store.Add((nuint)i);
                        store.TryGetAddress(reference, out _);
                    }
                },
                cancellationToken));

            var clearer = Task.Run(
                () =>
                {
                    for (int i = 0; i < 10_000; i++)
                    {
                        store.Clear();
                    }
                },
                cancellationToken);

            await Task.WhenAll(adders.Append(clearer));
        }
    }
}
