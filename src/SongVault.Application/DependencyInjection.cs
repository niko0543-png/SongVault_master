using Microsoft.Extensions.DependencyInjection;

using SongVault.Application._Temporary;
using SongVault.Application.Songs.CreateSong;
using SongVault.Application.Songs.GetSong;
using SongVault.Application.Songs.ListSongs;

namespace SongVault.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<CreateSongHandler>();
        services.AddScoped<GetSongHandler>();
        services.AddScoped<ListSongsHandler>();

        services.AddSingleton<InMemorySongStore>(); // TEMPORAIRE
        return services;
    }
}