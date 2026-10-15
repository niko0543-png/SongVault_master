using Microsoft.Extensions.DependencyInjection;
using SongVault.Application.Notifications;
using SongVault.Application.Abstractions;
using SongVault.Application.Accounts;
using SongVault.Application.Activity;
using SongVault.Application.Bands;
using SongVault.Application.Comments;
using SongVault.Application.Files;
using SongVault.Application.Invitations;
using SongVault.Application.Releases;
using SongVault.Application.Songs.CreateSong;
using SongVault.Application.Songs.DeleteSong;
using SongVault.Application.Songs.GetSong;
using SongVault.Application.Songs.ListSongs;
using SongVault.Application.Songs.UpdateSong;
using SongVault.Application.Versions;
using SongVault.Application.Sharing;

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
        // #5 : export des données du compte (ADR 0011)
        services.AddScoped<RequestDataExportHandler>();
        services.AddScoped<GetLatestDataExportHandler>();
        services.AddScoped<DownloadDataExportHandler>();
        services.AddScoped<IBackgroundJobHandler, ExportAccountJob>();
        services.AddScoped<IBackgroundJobHandler, ExpireExportJob>();
        // #15 : forme d'onde et aperçu audio (ADR 0021)
        services.AddScoped<AudioPreviewQueue>();
        services.AddScoped<IBackgroundJobHandler, AudioPreviewJob>();
        services.AddScoped<DeleteAccountHandler>();
        services.AddScoped<IBackgroundJobHandler, PurgeAccountJob>();
        // #6 : envoi direct vers le stockage (ADR 0012)
        services.AddScoped<StartFileUploadHandler>();
        services.AddScoped<ReceiveFileUploadHandler>();
        services.AddScoped<CompleteFileUploadHandler>();
        services.AddScoped<IBackgroundJobHandler, ExpireFileUploadJob>();
        // #8 : commentaires horodatés (ADR 0014)
        services.AddScoped<ListCommentsHandler>();
        services.AddScoped<CreateCommentHandler>();
        services.AddScoped<ReplyToCommentHandler>();
        services.AddScoped<EditCommentHandler>();
        services.AddScoped<DeleteCommentHandler>();
        services.AddScoped<ResolveCommentHandler>();
        // #10 : sorties (ADR 0016)
        services.AddScoped<ListReleasesHandler>();
        services.AddScoped<GetReleaseHandler>();
        services.AddScoped<CreateReleaseHandler>();
        services.AddScoped<UpdateReleaseHandler>();
        services.AddScoped<DeleteReleaseHandler>();
        services.AddScoped<SetReleaseTracksHandler>();
        services.AddScoped<UploadReleaseCoverHandler>();
        services.AddScoped<DownloadReleaseCoverHandler>();
        services.AddScoped<DeleteReleaseCoverHandler>();
        // #11 : fil d'activité et badges (ADR 0017)
        services.AddScoped<ActivityLog>();
        services.AddScoped<ListActivityHandler>();
        services.AddScoped<MarkSongSeenHandler>();
        // #12 : notifications par e-mail (ADR 0018)
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<IBackgroundJobHandler, SendNotificationJob>();
        services.AddScoped<IBackgroundJobHandler, NotificationDigestJob>();
        services.AddScoped<GetNotificationPreferencesHandler>();
        services.AddScoped<UpdateNotificationPreferencesHandler>();
        services.AddScoped<UnsubscribeHandler>();
        // #13 : liens de partage externes (ADR 0019)
        services.AddScoped<CreateShareLinkHandler>();
        services.AddScoped<ListShareLinksHandler>();
        services.AddScoped<RevokeShareLinkHandler>();
        services.AddScoped<DeleteShareLinkHandler>();
        services.AddScoped<ListShareLinkAccessesHandler>();
        services.AddScoped<ShareLinkGate>();
        services.AddScoped<ShareAccessLog>();
        services.AddScoped<OpenSharedContentHandler>();
        services.AddScoped<UnlockShareLinkHandler>();
        services.AddScoped<DownloadSharedFileHandler>();
        services.AddScoped<DownloadSharedCoverHandler>();

        return services;
    }
}