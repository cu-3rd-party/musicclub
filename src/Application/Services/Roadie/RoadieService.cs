using System.Net;
using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.Roadie;

public class RoadieService(
    ISongRepository songs,
    IRoadieTicketRepository tickets,
    ISongRoadieRepository roadies,
    IApplicationUserRepository users,
    IPermissionService permissions,
    ITelegramChatService telegram,
    ISongTopicRepository songTopics) : IRoadieService
{
    private static readonly TimeSpan AutoAssignAge = TimeSpan.FromHours(24);

    public async Task<RoadieTicketDto> CreateTicketAsync(Guid songId,
        ApplicationUser currentUser,
        RoadieTicketType ticketType = RoadieTicketType.Help,
        CancellationToken cancellationToken = default)
    {
        var song = await songs.FindByIdWithDetailsAsync(songId, cancellationToken) ??
                   throw new NotFoundException(songId.ToString(), nameof(Song));

        var requesterId = currentUser.Id;

        var isParticipant = song.CreatedById == requesterId || song.Assignments.Any(a => a.UserId == requesterId);
        if (!isParticipant) throw new ForbiddenAccessException();

        // Дубль открытой заявки — возвращаем существующую, без повторных уведомлений.
        if (await tickets.HasOpenTicketAsync(songId, ticketType: ticketType, ct: cancellationToken))
        {
            var existing = tickets
                .Query()
                .FirstOrDefault(t => t.SongId == songId && t.AcceptedById == null && t.RoadieTicketType == ticketType);
            if (existing is not null) return ToDto(existing, song);
        }

        var ticket = new RoadieTicket
        {
            Id = Guid.NewGuid(),
            SongId = songId,
            RoadieTicketType = ticketType,
            CreatedById = requesterId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await tickets.AddAsync(ticket, cancellationToken);
        await tickets.SaveChangesAsync(cancellationToken);

        var topic = await songTopics.FindBySongIdAsync(songId, cancellationToken);
        if (topic is not null)
            await telegram.SendTopicMessage(topic.TopicId,
                $"🔎 Группе «{WebUtility.HtmlEncode(song.Title)}» сейчас ищется роуди.",
                cancellationToken);

        await telegram.SendRoadieTicketNotification(ticket.Id,
            song.Title,
            song.Artist,
            song.Roles,
            ticketType,
            cancellationToken);

        return ToDto(ticket, song);
    }

    public async Task<RoadieAcceptResult> AcceptTicketAsync(long tgUserId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByTgUserIdAsync(tgUserId, cancellationToken);
        if (user is null) return RoadieAcceptResult.NotARoadie;

        var perms = await permissions.GetPermissionValuesAsync(user, cancellationToken);
        if (!perms.Contains(CuMusicClub.Domain.Constants.Permission.RoadieManage)) return RoadieAcceptResult.NotARoadie;

        var ticket = await tickets.GetByIdWithDetailsAsync(ticketId, cancellationToken);
        if (ticket is null) return RoadieAcceptResult.NotFound;

        if (ticket.AcceptedById is not null) return RoadieAcceptResult.AlreadyAccepted;

        ticket.AcceptedById = user.Id;
        ticket.AcceptedAt = DateTimeOffset.UtcNow;
        tickets.Update(ticket);

        if (ticket.RoadieTicketType == RoadieTicketType.Assignment)
            await roadies.AddAsync(new SongRoadie
                {
                    SongId = ticket.SongId,
                    RoadieId = user.Id,
                    AssignedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);

        await tickets.SaveChangesAsync(cancellationToken);

        var topic = await songTopics.FindBySongIdAsync(ticket.SongId, cancellationToken);
        if (topic is null) return RoadieAcceptResult.Accepted;

        switch (ticket.RoadieTicketType)
        {
            case RoadieTicketType.Assignment:
                await telegram.SendTopicMessage(topic.TopicId,
                    $"🎸 Ваш постоянный роуди — {telegram.BuildUserMention(user)}.",
                    cancellationToken);
                break;
            case RoadieTicketType.Help:
                await telegram.SendTopicMessage(topic.TopicId,
                    $"🎸 По вашему запросу был выдан временный роуди — {telegram.BuildUserMention(user)}.",
                    cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return RoadieAcceptResult.Accepted;
    }

    public async Task<int> AutoAssignOpenTicketsAsync(CancellationToken cancellationToken = default)
    {
        var olderThan = DateTimeOffset.UtcNow.Add(-AutoAssignAge);
        var overdue = await tickets.GetOpenTicketsOlderThanAsync(olderThan, cancellationToken);
        if (overdue.Count == 0) return 0;

        var roadieUsers =
            await users.GetUsersByPermissionAsync(Domain.Constants.Permission.RoadieAutoAssign, cancellationToken);
        if (roadieUsers.Count == 0) return 0;

        var allRoadies = roadies
            .Query()
            .ToList();
        var roadieSongIds = allRoadies
            .Select(r => r.SongId)
            .ToHashSet();
        var load = allRoadies
            .GroupBy(r => r.RoadieId)
            .ToDictionary(g => g.Key, g => g.Count());

        var count = 0;
        foreach (var ticket in overdue)
        {
            // У песни уже есть роуди (или на него уже назначена другая заявка в этом проходе) —
            // пропускаем: не вешаем второго роуди и не шлём повторное уведомление.
            if (!roadieSongIds.Add(ticket.SongId)) continue;

            var candidates = roadieUsers
                .Where(u => u.TgUserId != null)
                .ToList();
            if (candidates.Count == 0) candidates = roadieUsers.ToList();
            if (candidates.Count == 0) continue;

            var chosen = candidates
                .OrderBy(u => load.GetValueOrDefault(u.Id))
                .ThenBy(_ => Random.Shared.Next())
                .First();

            ticket.AcceptedById = chosen.Id;
            ticket.AcceptedAt = DateTimeOffset.UtcNow;
            tickets.Update(ticket);

            await roadies.AddAsync(new SongRoadie
                {
                    SongId = ticket.SongId,
                    RoadieId = chosen.Id,
                    AssignedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);
            count++;

            var song = ticket.Song ?? await songs.FindByIdAsync(ticket.SongId);
            var songTitle = WebUtility.HtmlEncode(song?.Title ?? "?");

            if (chosen.TgUserId is not null)
            {
                await telegram.SendDirectMessage(chosen.TgUserId.Value,
                    $"🎸 Вам автоматически назначена группа «{songTitle}».",
                    cancellationToken);
            }
            else
            {
                await telegram.SendRoadieMessage($"Группе «{songTitle}» назначен роуди (у пользователя нет TgUserId).",
                    cancellationToken);
            }

            var topic = await songTopics.FindBySongIdAsync(ticket.SongId, cancellationToken);
            if (topic is not null)
                await telegram.SendTopicMessage(topic.TopicId,
                    $"🎸 Ваш роуди — {telegram.BuildUserMention(chosen)}.",
                    cancellationToken);
        }

        await tickets.SaveChangesAsync(cancellationToken);
        return count;
    }

    public async Task<IEnumerable<ApplicationUser>> ListRoadies(CancellationToken cancellationToken = default)
    {
        return await users.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieManage,
            cancellationToken);
    }

    public async Task<ApplicationUser?> GetRoadie(Domain.Entities.Song song,
        CancellationToken cancellationToken = default)
    {
        var roadie = await roadies.FindBySongIdAsync(song.Id, cancellationToken);
        return roadie?.Roadie;
    }

    public async Task AssignRoadieAsync(Guid songId,
        ApplicationUser targetUser,
        ApplicationUser actor,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoadieManagerAsync(actor, cancellationToken);

        var targetPerms = await permissions.GetPermissionValuesAsync(targetUser, cancellationToken);
        if (!targetPerms.Contains(CuMusicClub.Domain.Constants.Permission.RoadieManage))
            throw new ForbiddenAccessException();

        var song = await songs.FindByIdAsync(songId, cancellationToken) ??
                   throw new NotFoundException(songId.ToString(), nameof(Song));

        var existing = await roadies.FindBySongIdAsync(songId, cancellationToken);
        if (existing is not null) roadies.Remove(existing);

        await roadies.AddAsync(new SongRoadie
            {
                SongId = song.Id,
                RoadieId = targetUser.Id,
                AssignedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);
        await roadies.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveRoadieAsync(Guid songId,
        ApplicationUser actor,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoadieManagerAsync(actor, cancellationToken);

        var song = await songs.FindByIdAsync(songId, cancellationToken) ??
                   throw new NotFoundException(songId.ToString(), nameof(Song));

        var existing = await roadies.FindBySongIdAsync(songId, cancellationToken);
        if (existing is null) return;

        roadies.Remove(existing);
        await roadies.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetRoadieCandidatesAsync(string? query,
        CancellationToken cancellationToken = default)
    {
        var candidates = await users.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieManage,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(query))
            return candidates
                .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        var lowered = query
            .Trim()
            .ToLowerInvariant();
        return candidates
            .Where(u => u
                            .DisplayName.ToLower()
                            .Contains(lowered) ||
                        (u.UserName != null &&
                         u
                             .UserName.ToLower()
                             .Contains(lowered)))
            .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task EnsureRoadieManagerAsync(ApplicationUser actor, CancellationToken cancellationToken)
    {
        var perms = await permissions.GetPermissionValuesAsync(actor, cancellationToken);
        if (!perms.Contains(CuMusicClub.Domain.Constants.Permission.RoadieManage)) throw new ForbiddenAccessException();
    }

    private static RoadieTicketDto ToDto(RoadieTicket ticket, CuMusicClub.Domain.Entities.Song song)
    {
        return new RoadieTicketDto(ticket.Id,
            ticket.SongId,
            song.Title,
            song.Artist,
            ticket.AcceptedById == null,
            ticket.AcceptedById,
            ticket.CreatedAt,
            ticket.AcceptedAt);
    }
}
