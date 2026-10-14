using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, SlaPolicy sla) : ITicketQueries
{
    /// <remarks>
    /// Filters, then counts, then sorts and pages, all in the database. The order matters: the
    /// count must describe the filtered set, or the pager lies.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        // One "now" for the whole request, so the SLA filter and the status shown on each row
        // can never disagree about what time it is.
        var now = clock.UtcNow;
        var atRiskFraction = sla.AtRiskFraction;

        var filtered = ApplyFilters(TicketsWithLabels(), query, now, atRiskFraction);

        var totalCount = await filtered.CountAsync(ct);

        var items = await ApplySort(filtered, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, atRiskFraction)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var atRiskFraction = sla.AtRiskFraction;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, atRiskFraction)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var atRiskFraction = sla.AtRiskFraction;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, atRiskFraction)
            })
            .ToListAsync(ct);
    }

    /// <remarks>
    /// Newest first, with the agents' names joined in on the read side. The ticket id leads the
    /// <c>IX_TicketEscalations_TicketId_EscalatedAtUtc</c> index and the time is its second
    /// column, so both the lookup and the ordering are served by the index. Two small queries
    /// (does the ticket exist, then its history) keep "no such ticket" apart from "no history".
    /// </remarks>
    public async Task<IReadOnlyList<TicketEscalationDto>?> GetEscalationsAsync(int ticketId, CancellationToken ct)
    {
        var ticketExists = await db.Set<Ticket>().AsNoTracking().AnyAsync(t => t.Id == ticketId, ct);

        if (!ticketExists)
        {
            return null;
        }

        return await (
            from escalation in db.Set<TicketEscalation>().AsNoTracking()
            where escalation.TicketId == ticketId
            from fromAgent in db.Set<Agent>().Where(a => a.Id == escalation.FromAgentId).DefaultIfEmpty()
            from toAgent in db.Set<Agent>().Where(a => a.Id == escalation.ToAgentId).DefaultIfEmpty()
            orderby escalation.EscalatedAtUtc descending, escalation.Id descending
            select new TicketEscalationDto(
                escalation.Id,
                escalation.FromPriority,
                escalation.ToPriority,
                escalation.FromDueAtUtc,
                escalation.ToDueAtUtc,
                fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                escalation.Reason,
                escalation.EscalatedBy,
                escalation.EscalatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    /// <summary>
    /// One <c>if</c> per filter, all ANDed. Everything stays an <see cref="IQueryable{T}"/>, so
    /// the database does the work. A further filter is one more <c>if</c> here plus a property
    /// on <see cref="TicketQuery"/>.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        DateTime now,
        double atRiskFraction)
    {
        if (query.Status is { } status)
        {
            source = source.Where(x => x.Ticket.Status == status);
        }

        if (query.Priority is { } priority)
        {
            source = source.Where(x => x.Ticket.Priority == priority);
        }

        if (query.CategoryId is { } categoryId)
        {
            source = source.Where(x => x.Ticket.CategoryId == categoryId);
        }

        if (query.CustomerId is { } customerId)
        {
            source = source.Where(x => x.Ticket.CustomerId == customerId);
        }

        if (query.AssignedAgentId is { } agentId)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == agentId);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == null);
        }

        // Search is trimmed and blank-to-null in TicketQuery.Normalized(); the guard here keeps a
        // query that skipped normalization from turning "   " into a match-nothing filter.
        if (query.Search?.Trim() is { Length: > 0 } search)
        {
            source = source.Where(x =>
                x.Ticket.Title.Contains(search) ||
                x.Ticket.Reference.Contains(search) ||
                x.Customer.Name.Contains(search));
        }

        if (query.SlaStatus is { } slaStatus)
        {
            source = source.Where(SlaStatusIs(slaStatus, now, atRiskFraction));
        }

        return source;
    }

    /// <summary>
    /// The SQL form of <see cref="SlaEvaluator.Evaluate"/>: the same five statuses, worked out
    /// by the database so that filtering, counting and paging all see the same rows.
    /// </summary>
    /// <remarks>
    /// <c>now</c> is the same value the page projection hands to the evaluator. The two
    /// open-ticket statuses need date arithmetic, so they use <c>DATEDIFF(second, ...)</c>
    /// (<c>EF.Functions.DateDiffSecond</c>), which only SQL Server can run. It counts
    /// whole seconds, so right on the threshold the two can differ by up to a second.
    /// </remarks>
    private static Expression<Func<TicketWithLabels, bool>> SlaStatusIs(
        SlaStatus status,
        DateTime now,
        double atRiskFraction)
    {
        switch (status)
        {
            case SlaStatus.NotApplicable:
                return x => x.Ticket.DueAtUtc == null;

            case SlaStatus.Met:
                return x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc != null &&
                    x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc;

            case SlaStatus.Breached:
                return x =>
                    x.Ticket.DueAtUtc != null &&
                    ((x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                     (x.Ticket.ResolvedAtUtc == null && now > x.Ticket.DueAtUtc));

            case SlaStatus.AtRisk:
                // Open, not yet past due, and no more than the threshold share of the window left.
                return x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc == null &&
                    x.Ticket.DueAtUtc >= now &&
                    EF.Functions.DateDiffSecond(now, x.Ticket.DueAtUtc) <=
                        atRiskFraction * EF.Functions.DateDiffSecond(x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc);

            case SlaStatus.WithinSla:
                // Open, not yet past due, and more than the threshold share of the window left.
                return x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc == null &&
                    x.Ticket.DueAtUtc >= now &&
                    EF.Functions.DateDiffSecond(now, x.Ticket.DueAtUtc) >
                        atRiskFraction * EF.Functions.DateDiffSecond(x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc);

            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown SLA status.");
        }
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
