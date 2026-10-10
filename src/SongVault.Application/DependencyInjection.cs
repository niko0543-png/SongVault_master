using Microsoft.Extensions.DependencyInjection;

using SongVault.Application.Bands;
using SongVault.Application.Files;
using SongVault.Application.Invitations;
using SongVault.Application.Songs.CreateSong;
using SongVault.Application.Songs.DeleteSong;
using SongVault.Application.Songs.GetSong;
using SongVault.Application.Songs.ListSongs;
using SongVault.Application.Songs.UpdateSong;
using SongVault.Application.Versions;

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
        services.AddScoped<CreateSongVersionHandler>();
        services.AddScoped<ListSongVersionsHandler>();
        services.AddScoped<GetSongVersionHandler>();
        services.AddScoped<UpdateSongVersionHandler>();
        services.AddScoped<DeleteSongHandler>();
        services.AddScoped<DeleteSongFileHandler>();
        services.AddScoped<DownloadSongFileHandler>();
        services.AddScoped<UploadSongFileHandler>();
        services.AddScoped<ListMyBandsHandler>();
        services.AddScoped<CreateBandHandler>();
        services.AddScoped<RenameBandHandler>();
        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<ListInvitationsHandler>();
        services.AddScoped<RevokeInvitationHandler>();
        services.AddScoped<PreviewInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<ListMembersHandler>();
        services.AddScoped<ChangeMemberRoleHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<LeaveBandHandler>();

        return services;
    }
}