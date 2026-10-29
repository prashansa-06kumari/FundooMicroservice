using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Labels;
using Fundoo.Notes.Application.Notes;
using Fundoo.Notes.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fundoo.Notes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("NotesDb")
            ?? throw new InvalidOperationException("ConnectionStrings:NotesDb is missing.");

        // One data source for the whole app (Npgsql handles connection pooling itself)
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<ILabelService, LabelService>();
        return services;
    }
}