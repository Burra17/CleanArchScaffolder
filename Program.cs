using Spectre.Console;

// ── Rubrik ──────────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(
    new FigletText("Clean Arch CLI")
        .Centered()
        .Color(Color.CornflowerBlue));

AnsiConsole.Write(
    new Rule("[dim]Scaffolda en Clean Architecture-lösning på nolltid[/]")
        .RuleStyle(Style.Parse("grey"))
        .Centered());
AnsiConsole.WriteLine();

// ── Input ───────────────────────────────────────────────────────
var solutionName = AnsiConsole.Prompt(
    new TextPrompt<string>("  Vad ska din nya [cornflowerblue]Solution[/] heta?")
        .ValidationErrorMessage("[red]Namnet får inte vara tomt![/]")
        .Validate(name =>
            string.IsNullOrWhiteSpace(name) ? ValidationResult.Error() :
            name.Contains(' ')              ? ValidationResult.Error("[red]Inga mellanslag tillåtna[/]") :
            ValidationResult.Success()));

AnsiConsole.WriteLine();

// ── Definiera lager ─────────────────────────────────────────────
var layers = new (string Suffix, string Template, string Icon, string Color)[]
{
    ("Domain",         "classlib", "[yellow]*[/]", "yellow"),
    ("Application",    "classlib", "[blue]*[/]",  "blue"),
    ("Infrastructure", "classlib", "[green]*[/]", "green"),
    ("Api",            "webapi",   "[red]*[/]",   "red"),
};

// ── Scaffold ────────────────────────────────────────────────────
var success = true;
string? slnFile = null;

AnsiConsole.Status()
    .Spinner(Spinner.Known.BouncingBall)
    .SpinnerStyle(Style.Parse("cornflowerblue"))
    .Start("Förbereder...", ctx =>
    {
        // 1. Skapa solution
        ctx.Status("[cornflowerblue]Skapar solution...[/]");
        if (!Run($"dotnet new sln -n {solutionName} --force"))
        { success = false; return; }

        // Hitta solution-filen (.slnx i .NET 10+, .sln i äldre)
        slnFile = Directory.GetFiles(".", $"{solutionName}.slnx").FirstOrDefault()
                   ?? Directory.GetFiles(".", $"{solutionName}.sln").FirstOrDefault();

        if (slnFile is null)
        {
            AnsiConsole.MarkupLine("[red]Kunde inte hitta skapade solution-filen![/]");
            success = false; return;
        }

        // 2. Skapa .gitignore
        ctx.Status("[dim]Skapar .gitignore...[/]");
        Run("dotnet new gitignore --force");

        // 3. Skapa varje lager-projekt
        foreach (var (suffix, template, icon, color) in layers)
        {
            var projectName = $"{solutionName}.{suffix}";
            ctx.Status($"[{color}]{icon} Skapar {projectName}...[/]");

            if (!Run($"dotnet new {template} -n {projectName} -o src/{projectName} --force"))
            { success = false; return; }

            if (!Run($"dotnet sln {slnFile} add src/{projectName}/{projectName}.csproj"))
            { success = false; return; }
        }

        // 4. Skapa testprojekt
        var testProjectName = $"{solutionName}.Application.Tests";
        ctx.Status("[magenta]Skapar testprojekt...[/]");

        if (!Run($"dotnet new xunit -n {testProjectName} -o tests/{testProjectName} --force"))
        { success = false; return; }

        if (!Run($"dotnet sln {slnFile} add tests/{testProjectName}/{testProjectName}.csproj"))
        { success = false; return; }

        Run($"dotnet add tests/{testProjectName}/{testProjectName}.csproj reference src/{solutionName}.Application/{solutionName}.Application.csproj");

        ctx.Status("[magenta]Installerar testpaket...[/]");
        Run($"dotnet add tests/{testProjectName}/{testProjectName}.csproj package FluentAssertions");

        // 5. Sätt upp projektreferenser (ren beroendekedja)
        ctx.Status("[dim]Kopplar ihop projekten...[/]");

        // Api → Infrastructure (Application nås transitivt)
        Run($"dotnet add src/{solutionName}.Api/{solutionName}.Api.csproj reference src/{solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj");

        // Infrastructure → Application
        Run($"dotnet add src/{solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj reference src/{solutionName}.Application/{solutionName}.Application.csproj");

        // Application → Domain
        Run($"dotnet add src/{solutionName}.Application/{solutionName}.Application.csproj reference src/{solutionName}.Domain/{solutionName}.Domain.csproj");

        // 6. Installera NuGet-paket
        var api = $"src/{solutionName}.Api/{solutionName}.Api.csproj";
        var app = $"src/{solutionName}.Application/{solutionName}.Application.csproj";
        var infra = $"src/{solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj";

        ctx.Status("[green]Installerar EF Core i Infrastructure...[/]");
        Run($"dotnet add {infra} package Microsoft.EntityFrameworkCore.SqlServer");
        Run($"dotnet add {infra} package Microsoft.EntityFrameworkCore.Tools");

        ctx.Status("[red]Installerar paket i Api...[/]");
        Run($"dotnet add {api} package Microsoft.EntityFrameworkCore.Design");
        Run($"dotnet add {api} package Scalar.AspNetCore");

        ctx.Status("[blue]Installerar MediatR & FluentValidation i Application...[/]");
        Run($"dotnet add {app} package MediatR");
        Run($"dotnet add {app} package FluentValidation");
        Run($"dotnet add {app} package FluentValidation.DependencyInjectionExtensions");

        // 7. Städa bort genererade filer
        ctx.Status("[dim]Städar bort genererade filer...[/]");
        foreach (var (suffix, _, _, _) in layers)
        {
            var classFile = Path.Combine("src", $"{solutionName}.{suffix}", "Class1.cs");
            if (File.Exists(classFile))
                File.Delete(classFile);
        }

        var testDir = Path.Combine("tests", testProjectName);
        var unitTest1 = Path.Combine(testDir, "UnitTest1.cs");
        if (File.Exists(unitTest1))
            File.Delete(unitTest1);

        var apiDir = Path.Combine("src", $"{solutionName}.Api");
        string[] weatherFiles =
        [
            Path.Combine(apiDir, "WeatherForecast.cs"),
            Path.Combine(apiDir, "Controllers", "WeatherForecastController.cs"),
        ];
        foreach (var file in weatherFiles)
        {
            if (File.Exists(file))
                File.Delete(file);
        }

        // 8. Skapa mappstruktur
        ctx.Status("[dim]Skapar mappstruktur...[/]");

        string[][] folders =
        [
            [$"src/{solutionName}.Domain/Entities", $"src/{solutionName}.Domain/ValueObjects", $"src/{solutionName}.Domain/Enums"],
            [$"src/{solutionName}.Application/Interfaces", $"src/{solutionName}.Application/Features", $"src/{solutionName}.Application/DTOs"],
            [$"src/{solutionName}.Infrastructure/Persistence", $"src/{solutionName}.Infrastructure/Repositories"],
            [$"src/{solutionName}.Api/Controllers"],
        ];

        foreach (var group in folders)
            foreach (var folder in group)
            {
                Directory.CreateDirectory(folder);
                // Skapa .gitkeep så att tomma mappar syns i VS och Git
                var gitkeep = Path.Combine(folder, ".gitkeep");
                if (!File.Exists(gitkeep))
                    File.WriteAllBytes(gitkeep, []);
            }

        // 9. Skapa IAppDbContext i Application
        ctx.Status("[blue]Skapar interfaces...[/]");
        File.WriteAllText(
            Path.Combine("src", $"{solutionName}.Application", "Interfaces", "IAppDbContext.cs"),
            $$"""
            namespace {{solutionName}}.Application.Interfaces;

            public interface IAppDbContext
            {
                Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
            }
            """);

        // 10. Skapa AppDbContext i Infrastructure
        ctx.Status("[green]Skapar AppDbContext...[/]");
        File.WriteAllText(
            Path.Combine("src", $"{solutionName}.Infrastructure", "Persistence", "AppDbContext.cs"),
            $$"""
            using Microsoft.EntityFrameworkCore;
            using {{solutionName}}.Application.Interfaces;

            namespace {{solutionName}}.Infrastructure.Persistence;

            public class AppDbContext(DbContextOptions<AppDbContext> options)
                : DbContext(options), IAppDbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
                    base.OnModelCreating(modelBuilder);
                }
            }
            """);

        // 11. Skapa DependencyInjection i Application
        ctx.Status("[blue]Skapar DI-registrering...[/]");
        File.WriteAllText(
            Path.Combine("src", $"{solutionName}.Application", "DependencyInjection.cs"),
            $$"""
            using FluentValidation;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{solutionName}}.Application;

            public static class DependencyInjection
            {
                public static IServiceCollection AddApplication(this IServiceCollection services)
                {
                    services.AddMediatR(cfg =>
                        cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

                    services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

                    return services;
                }
            }
            """);

        // 12. Skapa DependencyInjection i Infrastructure
        File.WriteAllText(
            Path.Combine("src", $"{solutionName}.Infrastructure", "DependencyInjection.cs"),
            $$"""
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;
            using {{solutionName}}.Application.Interfaces;
            using {{solutionName}}.Infrastructure.Persistence;

            namespace {{solutionName}}.Infrastructure;

            public static class DependencyInjection
            {
                public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
                {
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

                    services.AddScoped<IAppDbContext>(provider =>
                        provider.GetRequiredService<AppDbContext>());

                    return services;
                }
            }
            """);

        // 13. Skriv över Program.cs med DI-registrering
        ctx.Status("[red]Skriver Program.cs...[/]");
        File.WriteAllText(
            Path.Combine(apiDir, "Program.cs"),
            $$"""
            using Scalar.AspNetCore;
            using {{solutionName}}.Application;
            using {{solutionName}}.Infrastructure;

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseHttpsRedirection();
            app.MapControllers();
            app.Run();
            """);

        // 14. Lägg till connection string i appsettings.json
        ctx.Status("[dim]Uppdaterar appsettings.json...[/]");
        var appSettings = Path.Combine(apiDir, "appsettings.json");
        File.WriteAllText(appSettings, $$"""
            {
              "ConnectionStrings": {
                "DefaultConnection": "Server=.;Database={{solutionName}}Db;Trusted_Connection=true;TrustServerCertificate=true"
              },
              "Logging": {
                "LogLevel": {
                  "Default": "Information",
                  "Microsoft.AspNetCore": "Warning"
                }
              },
              "AllowedHosts": "*"
            }
            """);
    });

if (!success)
{
    AnsiConsole.MarkupLine("[bold red]Något gick fel -- kolla felmeddelandet ovan.[/]");
    return;
}

// ── Resultat ────────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[green bold]Klart![/]").RuleStyle(Style.Parse("green")));
AnsiConsole.WriteLine();

// Visa trädvy
var tree = new Tree($"[bold cornflowerblue]{Path.GetFileName(slnFile)}[/]")
    .Style(Style.Parse("dim"));

var srcNode = tree.AddNode("[dim]src/[/]");
var testsNode = tree.AddNode("[dim]tests/[/]");

// Domain
var domainNode = srcNode.AddNode($"[yellow bold]{solutionName}.Domain[/]");
domainNode.AddNode("[dim]Entities/[/]");
domainNode.AddNode("[dim]ValueObjects/[/]");
domainNode.AddNode("[dim]Enums/[/]");

// Application
var appNode = srcNode.AddNode($"[blue bold]{solutionName}.Application[/]");
appNode.AddNode("[dim]Interfaces/[/] [grey]IAppDbContext.cs[/]");
appNode.AddNode("[dim]Features/[/]");
appNode.AddNode("[dim]DTOs/[/]");
appNode.AddNode("[grey]DependencyInjection.cs[/]");

// Infrastructure
var infraNode = srcNode.AddNode($"[green bold]{solutionName}.Infrastructure[/]");
infraNode.AddNode("[dim]Persistence/[/] [grey]AppDbContext.cs[/]");
infraNode.AddNode("[dim]Repositories/[/]");
infraNode.AddNode("[grey]DependencyInjection.cs[/]");

// Api
var apiNode = srcNode.AddNode($"[red bold]{solutionName}.Api[/]");
apiNode.AddNode("[dim]Controllers/[/]");
apiNode.AddNode("[grey]Program.cs[/]");

// Tests
var testNode = testsNode.AddNode($"[magenta bold]{solutionName}.Application.Tests[/]");
testNode.AddNode("[dim](xUnit)[/]");

AnsiConsole.Write(
    new Panel(tree)
        .Header("[bold] Din nya lösning [/]")
        .Border(BoxBorder.Rounded)
        .BorderStyle(Style.Parse("cornflowerblue"))
        .Padding(1, 1));

AnsiConsole.WriteLine();

// Visa referenstabell
var table = new Table()
    .Border(TableBorder.Rounded)
    .BorderStyle(Style.Parse("grey"))
    .Title("[bold] Projektreferenser [/]")
    .AddColumn(new TableColumn("[bold]Projekt[/]").Centered())
    .AddColumn(new TableColumn("[bold]Beror på[/]").Centered());

table.AddRow($"[red]{solutionName}.Api[/]",              $"{solutionName}.Infrastructure");
table.AddRow($"[green]{solutionName}.Infrastructure[/]",  $"{solutionName}.Application");
table.AddRow($"[blue]{solutionName}.Application[/]",      $"{solutionName}.Domain");
table.AddRow($"[yellow]{solutionName}.Domain[/]",         "[dim]—[/]");
table.AddRow($"[magenta]{solutionName}.Application.Tests[/]", $"{solutionName}.Application");

AnsiConsole.Write(table);

AnsiConsole.WriteLine();

// Visa installerade paket
var pkgTable = new Table()
    .Border(TableBorder.Rounded)
    .BorderStyle(Style.Parse("grey"))
    .Title("[bold] Installerade NuGet-paket [/]")
    .AddColumn(new TableColumn("[bold]Projekt[/]").Centered())
    .AddColumn(new TableColumn("[bold]Paket[/]"));

pkgTable.AddRow(
    $"[green]{solutionName}.Infrastructure[/]",
    "EF Core SqlServer, EF Core Tools");
pkgTable.AddRow(
    $"[red]{solutionName}.Api[/]",
    "EF Core Design, Scalar.AspNetCore");
pkgTable.AddRow(
    $"[blue]{solutionName}.Application[/]",
    "MediatR, FluentValidation, FluentValidation.DependencyInjectionExtensions");
pkgTable.AddRow(
    $"[yellow]{solutionName}.Domain[/]",
    "[dim]—[/]");
pkgTable.AddRow(
    $"[magenta]{solutionName}.Application.Tests[/]",
    "xUnit, FluentAssertions");

AnsiConsole.Write(pkgTable);

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine($"[dim]Öppna [/][bold white]{Path.GetFileName(slnFile)}[/][dim] i Visual Studio för att komma igång![/]");
AnsiConsole.WriteLine();

// ── Hjälpfunktion ───────────────────────────────────────────────
bool Run(string command)
{
    var parts = command.Split(' ', 2);
    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = parts[0],
        Arguments = parts.Length > 1 ? parts[1] : "",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    });

    process!.WaitForExit();

    if (process.ExitCode != 0)
    {
        var error = process.StandardError.ReadToEnd();
        AnsiConsole.MarkupLine($"[red]Fel vid:[/] {command}");
        if (!string.IsNullOrWhiteSpace(error))
            AnsiConsole.MarkupLine($"[dim red]{Markup.Escape(error.Trim())}[/]");
    }

    return process.ExitCode == 0;
}
