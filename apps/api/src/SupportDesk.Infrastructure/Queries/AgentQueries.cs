using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class AgentQueries(SupportDbContext db) : IAgentQueries
{
    public async Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .OrderBy(a => a.FullName)
            .Select(a => new AgentDto(
                a.Id,
                a.FullName,
                a.Email,
                a.IsActive,
                a.MaxOpenTickets,
                db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed),
                (from specialization in a.Specializations
                 join category in db.Set<Category>() on specialization.CategoryId equals category.Id
                 select new CategorySummaryDto(category.Id, category.Name))
                .ToList()))
            .ToListAsync(ct);

    /// <remarks>
    /// One statement for every agent: the open count is a subquery and the specializations are
    /// joined in, so the cost does not grow with the number of agents. The projection stops at
    /// plain values; <see cref="AgentCandidate"/> is built from them in memory.
    /// </remarks>
    public async Task<IReadOnlyList<AgentCandidate>> GetAssignmentCandidatesAsync(CancellationToken ct)
    {
        var rows = await db.Set<Agent>()
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                OpenTickets = db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed),
                CategoryIds = a.Specializations.Select(s => s.CategoryId).ToList()
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new AgentCandidate(r.Id, r.FullName, r.IsActive, r.MaxOpenTickets, r.OpenTickets, r.CategoryIds))
            .ToList();
    }
}
