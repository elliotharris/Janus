#define TEST
using System.Collections.ObjectModel;
using Janus;
using Janus.Filters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public class DataStoreTests
    {
        private JanusData _data = new JanusData();

        private void Reset()
        {
            _data = new JanusData();
        }

        private Watcher AddWatcher(string watchDir, string syncDir, bool addFiles, bool deleteFiles,
            bool recursive, ObservableCollection<IFilter> filters = null)
        {
            if(filters == null) filters = new ObservableCollection<IFilter>();

            var watcher = new Watcher(
                "DataStoreTest",
                watchDir,
                syncDir,
                addFiles,
                deleteFiles,
                filters,
                recursive,
                observe: true
            );

            _data.Watchers.Add(watcher);

            return watcher;
        }

        private void RemoveWatcher(Watcher w)
        {
            _data.Watchers.Remove(w);
        }


        private int _count = 1;

        private void AssertLoadedWatchersAreCorrect(JanusData storedList)
        {
            Assert.AreEqual(storedList.Watchers.Count, _data.Watchers.Count,
                "[{0}] Number of watchers in loaded list does not match saved list.", _count);

            for (var i = 0; i < storedList.Watchers.Count; i++)
            {
                Assert.AreEqual(storedList.Watchers[i], _data.Watchers[i],
                    "[{0}-{1}] Stored watcher was not the same as saved watcher.", _count, i);

                CollectionAssert.AreEqual(storedList.Watchers[i].Data.Filters, _data.Watchers[i].Data.Filters,
                    "[{0}-{1}] Watcher filters were not the same.", _count, i);
            }

            _count++;
        }

        private void AssertDataProvidersAreEqual(JanusData storedList)
        {
            Assert.AreEqual(storedList.DataProvider.Dict.Count, _data.DataProvider.Dict.Count,
                "[{0}] Number of values in loaded data provider did not match.", _count);

            foreach (var kvp in _data.DataProvider.Dict)
            {
                AssertLoadedDataProviderContains(storedList, kvp.Key, kvp.Value);
            }
            _count++;
        }

        private void AssertLoadedDataProviderContains<T>(JanusData storedList, string key, T expected)
        {
            var data = storedList.DataProvider.Get<T>(key);
            Assert.AreEqual(data, expected,
                "[{0}] Loaded DataProvider value did not match.", _count);
        }

        private JanusData StoreAndLoad(DataStore testStore)
        {
            testStore.Store(_data);
            var storedList = testStore.Load();
            return storedList;
        }


        [TestMethod]
        public void DataStoreChange()
        {
            Reset();
            var testStore = new DataStore("test");

            Assert.IsFalse(string.IsNullOrEmpty(DataStore.AppData),
                "DataStore did not get the AppData folder.");
            Assert.IsFalse(string.IsNullOrEmpty(DataStore.AssemblyDirectory),
                "DataStore did not get the AssemblyDirectory.");
            Assert.IsFalse(string.IsNullOrEmpty(testStore.DataLocation),
                "DataStore did not set the DataLocation.");

            testStore.Initialise();

            Assert.IsTrue(testStore.DataLoaders.ContainsKey(DataStore.Version),
                "DataStore did not load a storage format matching the specified version: {0:X}", DataStore.Version);

            var w1 = AddWatcher("C:\\test\\directory", "C:\\out\\directory", false, false, true);
            var data = StoreAndLoad(testStore);
            AssertLoadedWatchersAreCorrect(data);

            RemoveWatcher(w1);
            data = StoreAndLoad(testStore);
            AssertLoadedWatchersAreCorrect(data);

            _data.DataProvider.Add("Test", "Hello World");
            data = StoreAndLoad(testStore);
            AssertLoadedWatchersAreCorrect(data);
            AssertLoadedDataProviderContains(data, "Test", "Hello World");

            var w2 = AddWatcher("C:\\test\\directory2", "C:\\out\\directory2", true, false, false);
            data = StoreAndLoad(testStore);
            AssertLoadedWatchersAreCorrect(data);
            AssertLoadedDataProviderContains(data, "Test", "Hello World");

            _data.DataProvider.Add("AnotherTest", true);
            data = StoreAndLoad(testStore);
            AssertLoadedWatchersAreCorrect(data);
            AssertLoadedDataProviderContains(data, "Test", "Hello World");
            AssertLoadedDataProviderContains(data, "AnotherTest", true);

            _data.DataProvider.Remove("Test");
            data = StoreAndLoad(testStore);
            AssertLoadedWatchersAreCorrect(data);
            AssertDataProvidersAreEqual(data);


            var w3 = AddWatcher("C:\\test\\directory3", "C:\\out\\directory3", false, true, true, new ObservableCollection<IFilter> { new IncludeFilter("test", "test2") });
            data = StoreAndLoad(testStore);
            AssertLoadedWatchersAreCorrect(data);
            AssertDataProvidersAreEqual(data);

            RemoveWatcher(w2);
            _data.DataProvider.Remove("AnotherTest");
            data = StoreAndLoad(testStore);
            AssertLoadedWatchersAreCorrect(data);
            AssertDataProvidersAreEqual(data);

            RemoveWatcher(w3);
            data = StoreAndLoad(testStore);
            AssertLoadedWatchersAreCorrect(data);
            AssertDataProvidersAreEqual(data);
        }

        [TestMethod]
        public void DataProviderTypes()
        {
            var testStore = new DataStore("test2");

            testStore.Initialise();

            _data.DataProvider.Add("tc-str", "Hello World");
            _data.DataProvider.Add("tc-int", 42);
            _data.DataProvider.Add("tc-neg-int", -123123);
            _data.DataProvider.Add("tc-bool-f", false);
            _data.DataProvider.Add("tc-bool-t", true);
            _data.DataProvider.Add("tc-double", 0.2342341234);
            _data.DataProvider.Add("tc-double-2", -1231231.231231);

            var data = StoreAndLoad(testStore);

            AssertDataProvidersAreEqual(data);
        }
    }
}