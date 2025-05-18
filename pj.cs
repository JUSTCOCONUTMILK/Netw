using FavoriteMoviesApp.Services;
using Microsoft.Extensions.DependencyInjection;
using FavoriteMoviesApp.Data;
using Microsoft.EntityFrameworkCore;

var services = new ServiceCollection();
services.AddScoped<AuthService>();
services.AddScoped<MovieService>();
services.AddScoped<FavoriteService>();
services.AddScoped<EmailService>();
services.AddScoped<PdfService>();

var provider = services.BuildServiceProvider();
var auth = provider.GetRequiredService<AuthService>();
var movieService = provider.GetRequiredService<MovieService>();
var favoriteService = provider.GetRequiredService<FavoriteService>();
var emailService = provider.GetRequiredService<EmailService>();
var pdfService = provider.GetRequiredService<PdfService>();

Console.WriteLine("1. Register\n2. Login");
var choice = Console.ReadLine();

string? userEmail = null;
if (choice == "1")
{
    Console.Write("Email: "); var email = Console.ReadLine();
    Console.Write("Password: "); var pass = Console.ReadLine();
    auth.Register(email!, pass!);
    userEmail = email;
}
else if (choice == "2")
{
    Console.Write("Email: "); var email = Console.ReadLine();
    Console.Write("Password: "); var pass = Console.ReadLine();
    if (auth.Login(email!, pass!)) userEmail = email;
    else Console.WriteLine("Invalid login");
}

if (userEmail != null)
{
    while (true)
    {
        Console.WriteLine("1. Search Movie\n2. Add to Favorites\n3. Export Favorites\n4. Exit");
        var cmd = Console.ReadLine();
        if (cmd == "1")
        {
            Console.Write("Search term: ");
            var term = Console.ReadLine();
            var results = movieService.Search(term!);
            foreach (var m in results) Console.WriteLine($"{m.Id}. {m.Title} - {m.Genre}");
        }
        else if (cmd == "2")
        {
            Console.Write("Movie ID to add: ");
            var id = int.Parse(Console.ReadLine()!);
            favoriteService.AddFavorite(userEmail, id);
        }
        else if (cmd == "3")
        {
            var favorites = favoriteService.GetFavorites(userEmail);
            var pdfs = pdfService.GeneratePdfs(favorites);
            emailService.SendWithAttachments(userEmail, "Your Favorite Movies", pdfs);
        }
        else break;
    }
}

namespace FavoriteMoviesApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public List<Favorite> Favorites { get; set; } = new();
    }
}

namespace FavoriteMoviesApp.Models
{
    public class Movie
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Genre { get; set; } = "";
    }
}

namespace FavoriteMoviesApp.Models
{
    public class Favorite
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public int MovieId { get; set; }
        public Movie Movie { get; set; }
    }
}

using Microsoft.EntityFrameworkCore;
using FavoriteMoviesApp.Models;

namespace FavoriteMoviesApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Movie> Movies => Set<Movie>();
        public DbSet<Favorite> Favorites => Set<Favorite>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            Database.EnsureCreated();
            if (!Movies.Any())
            {
                Movies.AddRange(new Movie { Title = "Inception", Genre = "Sci-Fi" },
                                new Movie { Title = "Titanic", Genre = "Romance" },
                                new Movie { Title = "The Matrix", Genre = "Action" },
                                new Movie { Title = "Up", Genre = "Animation" });
                SaveChanges();
            }
        }
    }
}

using BCrypt.Net;

namespace FavoriteMoviesApp.Utilities
{
    public static class PasswordHasher
    {
        public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
        public static bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
    }
}

using FavoriteMoviesApp.Data;
using FavoriteMoviesApp.Models;
using FavoriteMoviesApp.Utilities;

namespace FavoriteMoviesApp.Services
{
    public class AuthService
    {
        private readonly AppDbContext _ctx;
        public AuthService(AppDbContext ctx) => _ctx = ctx;

        public void Register(string email, string password)
        {
            if (_ctx.Users.Any(u => u.Email == email)) return;
            var user = new User { Email = email, PasswordHash = PasswordHasher.Hash(password) };
            _ctx.Users.Add(user);
            _ctx.SaveChanges();
        }

        public bool Login(string email, string password)
        {
            var user = _ctx.Users.FirstOrDefault(u => u.Email == email);
            return user != null && PasswordHasher.Verify(password, user.PasswordHash);
        }
    }
}

using FavoriteMoviesApp.Data;
using FavoriteMoviesApp.Models;

namespace FavoriteMoviesApp.Services
{
    public class MovieService
    {
        private readonly AppDbContext _ctx;
        public MovieService(AppDbContext ctx) => _ctx = ctx;

        public List<Movie> Search(string term) => _ctx.Movies.Where(m => m.Title.Contains(term) || m.Genre.Contains(term)).ToList();
    }
}

using FavoriteMoviesApp.Data;
using FavoriteMoviesApp.Models;

namespace FavoriteMoviesApp.Services
{
    public class FavoriteService
    {
        private readonly AppDbContext _ctx;
        public FavoriteService(AppDbContext ctx) => _ctx = ctx;

        public void AddFavorite(string email, int movieId)
        {
            var user = _ctx.Users.First(u => u.Email == email);
            var movie = _ctx.Movies.Find(movieId);
            if (!_ctx.Favorites.Any(f => f.UserId == user.Id && f.MovieId == movieId))
            {
                _ctx.Favorites.Add(new Favorite { User = user, Movie = movie! });
                _ctx.SaveChanges();
            }
        }

        public List<Movie> GetFavorites(string email)
        {
            return _ctx.Favorites.Where(f => f.User.Email == email).Select(f => f.Movie).ToList();
        }
    }
}

using FavoriteMoviesApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FavoriteMoviesApp.Services
{
    public class PdfService
    {
        public List<byte[]> GeneratePdfs(List<Movie> movies)
        {
            var pdfs = new List<byte[]>();
            for (int i = 0; i < movies.Count; i += 5)
            {
                var chunk = movies.Skip(i).Take(5).ToList();
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Content().Column(col =>
                        {
                            foreach (var movie in chunk)
                            {
                                col.Item().Text($"{movie.Title} - {movie.Genre}").FontSize(16);
                            }
                        });
                    });
                });
                pdfs.Add(doc.GeneratePdf());
            }
            return pdfs;
        }
    }
}

using System.Net.Mail;
using System.Net;

namespace FavoriteMoviesApp.Services
{
    public class EmailService
    {
        public void SendWithAttachments(string to, string subject, List<byte[]> attachments)
        {
            var message = new MailMessage("your@email.com", to, subject, "See attached PDFs.");
            message.IsBodyHtml = true;

            int count = 1;
            foreach (var pdf in attachments)
            {
                message.Attachments.Add(new Attachment(new MemoryStream(pdf), $"Favorites_{count++}.pdf"));
            }

            using var client = new SmtpClient("smtp.example.com")
            {
                Credentials = new NetworkCredential("your@email.com", "yourpassword"),
                EnableSsl = true,
                Port = 587
            };
            client.Send(message);
        }
    }
}

