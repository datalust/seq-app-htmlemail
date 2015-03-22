using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace Seq.App.EmailPlus.Tests
{
    [TestFixture]
    public class BatchingStreamTests
    {
        private TestScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _scheduler = new TestScheduler();
        }

        [Test]
        public void NullDelayDisablesBatching()
        {
            var stream = new BatchingStream<string, string>(value => value, _scheduler);

            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("foo"));

            var results = new List<IList<string>>();
            using (stream.Batches.Subscribe(list => results.Add(list)))
                _scheduler.Start();

            Assert.AreEqual(2, results.Count, "The wrong number of batches was returned.");
        }

        [Test]
        public void NonBatchedResultsIncludeOneItem()
        {
            var stream = new BatchingStream<string, string>(value => value, _scheduler);

            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("foo"));

            var results = new List<IList<string>>();
            using (stream.Batches.Subscribe(list => results.Add(list)))
                _scheduler.Start();

            foreach (var list in results)
                Assert.AreEqual(1, list.Count, "A non-batched result had more than one item.");
        }

        [Test]
        public void NonBatchedResultsIncludeCorrectValues()
        {
            var stream = new BatchingStream<string, string>(value => value, _scheduler);

            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("bar"));

            var results = new List<IList<string>>();
            using (stream.Batches.Subscribe(list => results.Add(list)))
                _scheduler.Start();

            Assert.AreEqual("foo", results[0][0], "The value of the first item was incorrect.");
            Assert.AreEqual("bar", results[1][0], "The value of the second item was incorrect.");
        }

        [Test]
        public void ItemsWithDifferentKeysAreNotBatched()
        {
            var stream = new BatchingStream<string, string>(value => value, _scheduler, TimeSpan.FromSeconds(2));

            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("bar"));

            var results = new List<IList<string>>();
            using (stream.Batches.Subscribe(list => results.Add(list)))
                _scheduler.Start();

            Assert.AreEqual(2, results.Count, "The wrong number of batches was returned.");
        }

        [Test]
        public void BatchedResultsHaveCorrectNumberOfItems()
        {
            var stream = new BatchingStream<string, string>(value => value, _scheduler, TimeSpan.FromSeconds(2));

            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("bar"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("bar"));

            var results = new List<IList<string>>();
            using (stream.Batches.Subscribe(list => results.Add(list)))
                _scheduler.Start();

            Assert.AreEqual(2, results[0].Count, "The first batch had the wrong number of items.");
            Assert.AreEqual(2, results[1].Count, "The second batch had the wrong number of items.");
        }

        [Test]
        public void BatchedResultsHaveCorrectValues()
        {
            var stream = new BatchingStream<string, string>(value => value, _scheduler, TimeSpan.FromSeconds(2));

            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("bar"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("bar"));

            var results = new List<IList<string>>();
            using (stream.Batches.Subscribe(list => results.Add(list)))
                _scheduler.Start();

            Assert.AreEqual("foo", results[0][0], "The value of the first item in the first batch was incorrect.");
            Assert.AreEqual("foo", results[0][1], "The value of the second item in the first batch was incorrect.");
            Assert.AreEqual("bar", results[1][0], "The value of the first item in the second batch was incorrect.");
            Assert.AreEqual("bar", results[1][1], "The value of the second item in the second batch was incorrect.");
        }

        [Test]
        public void BatchDelayIsHonored()
        {
            var stream = new BatchingStream<string, string>(value => value, _scheduler, TimeSpan.FromSeconds(1));

            _scheduler.Schedule(TimeSpan.FromSeconds(0), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2.01), () => stream.Add("foo"));

            var results = new List<IList<string>>();
            using (stream.Batches.Subscribe(list => results.Add(list)))
                _scheduler.Start();

            Assert.AreEqual(2, results.Count, "The wrong number of batches was returned.");
            Assert.AreEqual(2, results[0].Count, "The first batch has the wrong number of items.");
            Assert.AreEqual(1, results[1].Count, "The second batch has the wrong number of items.");
        }

        [Test]
        public void MaxDelayIsHonored()
        {
            var stream = new BatchingStream<string, string>(value => value, _scheduler, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

            _scheduler.Schedule(TimeSpan.FromSeconds(0), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2.01), () => stream.Add("foo"));

            var results = new List<IList<string>>();
            using (stream.Batches.Subscribe(list => results.Add(list)))
                _scheduler.Start();

            Assert.AreEqual(2, results.Count, "The wrong number of batches was returned.");
            Assert.AreEqual(3, results[0].Count, "The first batch has the wrong number of items.");
            Assert.AreEqual(1, results[1].Count, "The second batch has the wrong number of items.");
        }

        [Test]
        public void MaxSizeIsHonored()
        {
            var stream = new BatchingStream<string, string>(value => value, _scheduler, TimeSpan.FromSeconds(2), maxSize: 2);

            _scheduler.Schedule(TimeSpan.FromSeconds(0), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(1), () => stream.Add("foo"));
            _scheduler.Schedule(TimeSpan.FromSeconds(2), () => stream.Add("foo"));

            var results = new List<IList<string>>();
            using (stream.Batches.Subscribe(list => results.Add(list)))
                _scheduler.Start();

            Assert.AreEqual(2, results.Count, "The wrong number of batches was returned.");
            Assert.AreEqual(2, results[0].Count, "The first batch has the wrong number of items.");
            Assert.AreEqual(1, results[1].Count, "The second batch has the wrong number of items.");
        }
    }
}