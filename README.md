# Moq.EntityFramework
Mock your Entity Framework contexts with ease.

Inspired by code from https://msdn.microsoft.com/en-us/data/dn314429.aspx. The goal of this repo is to take all the repetitive parts out of mocking a DbSet.  

### How to use
1. Create your DbContext mock

	```
	var contextMock = DbContextMockFactory.Create<YourDbContext>();
	```
2. Add data to your set

	```
	var persons = Enumerable.Range(1, 100).Select(i => new Person {Id = i, Name = "Person " + i});
	var setMock = contextMock.MockedSet<Person>(persons); 
	```

3. Write your tests (assumes using NUnit and we're testing ASP.NET controllers)
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
		await controller.Post(new Person { Name = "John Lackey" });
		
		setMock.Verify(s => s.Add(It.IsAny<Person>()), Times.Once());
		mockContext.Verify(c => c.SaveChangesAsync(), Times.Once());
	}
	```

### TODOs
* Better test coverage
* Implement Find/FindAsync