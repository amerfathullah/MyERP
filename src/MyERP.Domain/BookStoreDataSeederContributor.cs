using System;
using System.Threading.Tasks;
using MyERP.Authors;
using MyERP.Books;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP;

public class MyERPDataSeederContributor
    : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Author, Guid> _authorRepository;
    private readonly IRepository<Book, Guid> _bookRepository;

    public MyERPDataSeederContributor(
        IRepository<Author, Guid> authorRepository,
        IRepository<Book, Guid> bookRepository)
    {
        _authorRepository = authorRepository;
        _bookRepository = bookRepository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        var georgeOrwell = await GetOrCreateAuthorAsync(
            "George Orwell",
            new DateTime(1903, 6, 25),
            "English novelist, essayist, journalist and critic.");

        var douglasAdams = await GetOrCreateAuthorAsync(
            "Douglas Adams",
            new DateTime(1952, 3, 11),
            "English author, humorist, and screenwriter.");

        if (await _bookRepository.GetCountAsync() <= 0)
        {
            await _bookRepository.InsertAsync(
                new Book
                {
                    Name = "1984",
                    AuthorId = georgeOrwell.Id,
                    Type = BookType.Dystopia,
                    PublishDate = new DateTime(1949, 6, 8),
                    Price = 19.84f
                },
                autoSave: true
            );

            await _bookRepository.InsertAsync(
                new Book
                {
                    Name = "The Hitchhiker's Guide to the Galaxy",
                    AuthorId = douglasAdams.Id,
                    Type = BookType.ScienceFiction,
                    PublishDate = new DateTime(1995, 9, 27),
                    Price = 42.0f
                },
                autoSave: true
            );
        }
    }

    private async Task<Author> GetOrCreateAuthorAsync(
        string name,
        DateTime birthDate,
        string shortBio)
    {
        var author = await _authorRepository.FindAsync(x => x.Name == name);
        if (author is not null)
        {
            return author;
        }

        return await _authorRepository.InsertAsync(
            new Author
            {
                Name = name,
                BirthDate = birthDate,
                ShortBio = shortBio
            },
            autoSave: true
        );
    }
}