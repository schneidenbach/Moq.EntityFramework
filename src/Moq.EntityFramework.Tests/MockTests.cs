using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Moq.EntityFramework.Tests
{
    [TestFixture]
    public class MockTests
    {
        public DbContextMock<TestContext> ContextMock { get; private set; }

        private IEnumerable<Person> CreatePersons()
        {
            return Enumerable.Range(1, 100).Select(i => new Person {Id = i, Name = "Person " + i});
        }

        [SetUp]
        public void Setup()
        {
            ContextMock = DbContextMockFactory
                            .Create<TestContext>()
                            .MockSetFor(CreatePersons().ToArray());
        }

        [Test]
        public void TestCollectedData()
        {
            var person = ContextMock.Object.Persons.SingleOrDefault(p => p.Id == 1);
            Assert.IsNotNull(person);
        }

        [Test]
        public void TestCount()
        {
            var count = ContextMock.Object.Persons.Count();
            Assert.That(count, Is.EqualTo(100));
        }

        [Test, Category("Ignore")]
        public void TestFind()
        {
            var person = ContextMock.MockedSet<Person>().Object.Find(3);
            Assert.That(person, Is.Not.Null);
        }
    }
}
