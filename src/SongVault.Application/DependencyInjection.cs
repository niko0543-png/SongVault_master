using Microsoft.Extensions.DependencyInjection;

using SongVault.Application.Songs.CreateSong;
using SongVault.Application.Songs.DeleteSong;
using SongVault.Application.Songs.GetSong;
using SongVault.Application.Songs.ListSongs;
using SongVault.Application.Songs.UpdateSong;

namespace SongVault.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<CreateSongHandler>();
        services.AddScoped<GetSongHandler>();
        services.AddScoped<ListSongsHandler>();
        services.AddScoped<UpdateSongHandler>();
        services.AddScoped<DeleteSongHandler>();

        return services;
    }
}