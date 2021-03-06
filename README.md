# Moq.EntityFramework
Mock your Entity Framework contexts with ease.

Inspired by code from https://msdn.microsoft.com/en-us/data/dn314429.aspx. The goal of this repo is to take all the repetitive parts out of mocking a DbSet.  

### How to use
1. Create your DbContext.  (Make sure you mark your DbSets as virtual - Moq requires this in order to create a mock of your class!)

	```
	public class YourDbContext : DbContext
	{
		public virtual DbSet<Person> Persons { get; set; }
	}
	```
2. Create your DbContext mock

	```
	var contextMock = DbContextMockFactory.Create<YourDbContext>();
	```
3. Add data to your set

	```
	var persons = Enumerable.Range(1, 100).Select(i => new Person {Id = i, Name = "Person " + i});
	var setMock = contextMock.MockedSet<Person>(persons); 
	```

4. Write your tests (assumes using NUnit and we're testing ASP.NET controllers)
	```
	public async Task GetTest()
	{
		var controller = new PersonsController(contextMock.Object);
		var getResult = await controller.GetAsync();

		Assert.That(getResult.Count(), Is.EqualTo(100));
		Assert.That(getResult.First().Id, Is.EqualTo(1));
	}
	```


	```
	public async Task PostTest()
	{
		var controller = new PersonsController(contextMock.Object);
		var newPerson = new Person { Name = "John Lackey" };
		await controller.Post(newPerson);
		
		setMock.Verify(s => s.Add(newPerson), Times.Once());
		mockContext.Verify(c => c.SaveChangesAsync(), Times.Once());
	}
	```

### Limitations
* As stated in the [blog post this repo is based on](https://msdn.microsoft.com/en-us/data/dn314429.aspx), any queries you do will default to using LINQ to Objects as opposed to LINQ to Entities.  This will sometimes result in different results based on your queries.
* Requires Entity Framework 6.  Untested with 7, would assume it would not work out of the box.

### TODOs
* Better test coverage
* Implement Find/FindAsync
