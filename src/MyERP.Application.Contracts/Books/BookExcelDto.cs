using System;

namespace MyERP.Books;

public class BookExcelDto
{
    public string Name { get; set; }

    public string AuthorName { get; set; }

    public BookType Type { get; set; }

    public DateTime PublishDate { get; set; }

    public float Price { get; set; }
}
