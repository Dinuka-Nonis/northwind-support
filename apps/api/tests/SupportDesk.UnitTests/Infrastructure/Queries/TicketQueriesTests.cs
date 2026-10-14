using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The ticket list filters, run against a real (in-memory SQLite) database so that what is
/// tested is the SQL EF Core generates, not a LINQ-to-objects imitation of it.
/// </summary>
/// <remarks>
/// SQLite translates <c>Contains</c> to a case-sensitive <c>instr</c>, whereas SQL Server's
/// default collation is case-insensitive. Search text in these tests therefore matches the case
/// of the seed data on purpose; do not "fix" it by changing the case.
/// </remarks>
public sealed class TicketQueriesFilterTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly CommandLog _commands = new();
    private readonly SupportDbContext _db;
    private readonly int _alexId;
    private readonly int _benId;
    private readonly int _fabrikamId;
    private readonly int _technicalId;

    public TicketQueriesFilterTests()
    {
        _connection.Open();

        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_commands)
            .Options);
        _db.Database.EnsureCreated();

        var now = FixedClock.DefaultNow;

        var fabrikam = new Customer("Fabrikam", "ops@fabrikam.test", null, CustomerTier.Standard, now);
        var contoso = new Customer("Contoso", "ops@contoso.test", null, CustomerTier.Premium, now);
        var billing = new Category("Billing", requiresSpecialist: false, forcesCriticalPriority: false);
        var technical = new Category("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
        var alex = new Agent("Alex Turner", "alex@support.test", 10, now);
        var ben = new Agent("Ben Osei", "ben@support.test", 8, now);

        // Foreign keys are enforced, so the rows a ticket points at go in first.
        _db.AddRange(fabrikam, contoso, billing, technical, alex, ben);
        _db.SaveChanges();

        _alexId = alex.Id;
        _benId = ben.Id;
        _fabrikamId = fabrikam.Id;
        _technicalId = technical.Id;

        // reference, title, customer, category, priority, status, agent
        Seed("TCK-0001", "Invoice shows the wrong total",    fabrikam, billing,   TicketPriority.High,     TicketStatus.Open,       alex);
        Seed("TCK-0002", "Cannot log in to the portal",      fabrikam, technical, TicketPriority.High,     TicketStatus.InProgress, ben);
        Seed("TCK-0003", "Invoice PDF will not download",    contoso,  billing,   TicketPriority.Low,      TicketStatus.New,        null);
        Seed("TCK-0004", "Portal is slow",                   contoso,  technical, TicketPriority.Medium,   TicketStatus.Open,       alex);
        Seed("TCK-0005", "Export fails with a timeout",      contoso,  technical, TicketPriority.Critical, TicketStatus.Open,       null);
        Seed("TCK-0042", "Password reset email is missing",  fabrikam, technical, TicketPriority.High,     TicketStatus.Open,       null);
        Seed("TCK-0007", "Update the billing address",       contoso,  billing,   TicketPriority.Medium,   TicketStatus.Resolved,   ben);
        Seed("TCK-0008", "Dashboard widget is broken",       fabrikam, technical, TicketPriority.Low,      TicketStatus.New,        null);

        _db.SaveChanges();

        // Seeding is not what the tests measure.
        _commands.Clear();
    }

    [Fact]
    public async Task NoFilters_ReturnsEveryTicket()
    {
        var result = await Run(new TicketQuery());

        Assert.Equal(8, result.TotalCount);
    }

    [Fact]
    public async Task Priority_IsAnExactMatch()
    {
        var result = await Run(new TicketQuery { Priority = TicketPriority.High });

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task Status_IsAnExactMatch()
    {
        var result = await Run(new TicketQuery { Status = TicketStatus.InProgress });

        var only = Assert.Single(result.Items);
        Assert.Equal("TCK-0002", only.Reference);
    }

    [Fact]
    public async Task CategoryAndCustomer_AreExactMatches()
    {
        var result = await Run(new TicketQuery { CategoryId = _technicalId, CustomerId = _fabrikamId });

        Assert.Equal(new[] { "TCK-0002", "TCK-0008", "TCK-0042" }, Refs(result).Order());
    }

    [Fact]
    public async Task AssignedAgent_IsAnExactMatch()
    {
        var result = await Run(new TicketQuery { AssignedAgentId = _benId });

        Assert.Equal(new[] { "TCK-0002", "TCK-0007" }, Refs(result).Order());
    }

    [Fact]
    public async Task UnassignedOnly_ReturnsOnlyTicketsWithoutAnAgent()
    {
        var result = await Run(new TicketQuery { UnassignedOnly = true });

        Assert.Equal(4, result.TotalCount);
        Assert.All(result.Items, item => Assert.Null(item.AssignedAgent));
    }

    [Fact]
    public async Task UnassignedOnlyWithAnAgent_IsEmpty_BecauseFiltersAreAnded()
    {
        var result = await Run(new TicketQuery { UnassignedOnly = true, AssignedAgentId = _alexId });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task CombinedFilters_AreAnded()
    {
        // Open AND High AND Alex: only TCK-0001 satisfies all three. TCK-0042 is Open and High
        // but unassigned; TCK-0004 is Open and Alex's but Medium; TCK-0002 is High but not Open.
        var result = await Run(new TicketQuery
        {
            Status = TicketStatus.Open,
            Priority = TicketPriority.High,
            AssignedAgentId = _alexId
        });

        var only = Assert.Single(result.Items);
        Assert.Equal("TCK-0001", only.Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_MatchesCustomerName()
    {
        var result = await Run(new TicketQuery { Search = "Fabrikam" });

        // None of these titles or references contains "Fabrikam": the match is the customer.
        Assert.Equal(new[] { "TCK-0001", "TCK-0002", "TCK-0008", "TCK-0042" }, Refs(result).Order());
        Assert.All(result.Items, item => Assert.Equal("Fabrikam", item.Customer.Name));
    }

    [Fact]
    public async Task Search_MatchesTheReference()
    {
        var full = await Run(new TicketQuery { Search = "TCK-0042" });
        var partial = await Run(new TicketQuery { Search = "0042" });

        Assert.Equal("TCK-0042", Assert.Single(full.Items).Reference);
        Assert.Equal("TCK-0042", Assert.Single(partial.Items).Reference);
    }

    [Fact]
    public async Task Search_MatchesTheTitle()
    {
        var result = await Run(new TicketQuery { Search = "Invoice" });

        Assert.Equal(new[] { "TCK-0001", "TCK-0003" }, Refs(result).Order());
    }

    [Fact]
    public async Task Search_IsTrimmed()
    {
        var result = await Run(new TicketQuery { Search = "  Fabrikam  " });

        Assert.Equal(4, result.TotalCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankSearch_IsIgnored(string search)
    {
        var result = await Run(new TicketQuery { Search = search });

        Assert.Equal(8, result.TotalCount);
    }

    [Fact]
    public async Task Search_TreatsWildcardCharactersAsLiteralText()
    {
        var percent = await Run(new TicketQuery { Search = "%" });
        var underscore = await Run(new TicketQuery { Search = "_" });

        Assert.Empty(percent.Items);
        Assert.Empty(underscore.Items);
    }

    [Fact]
    public async Task SearchAndFilter_AreAnded()
    {
        var result = await Run(new TicketQuery { Search = "Fabrikam", Priority = TicketPriority.Low });

        Assert.Equal("TCK-0008", Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task Count_DescribesTheFilteredSet_NotThePage()
    {
        var page1 = await Run(new TicketQuery { Priority = TicketPriority.High, PageSize = 2, Page = 1 });
        var page2 = await Run(new TicketQuery { Priority = TicketPriority.High, PageSize = 2, Page = 2 });

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);

        Assert.Equal(3, page2.TotalCount);
        Assert.Equal(1, page2.Items.Count);

        // Paging moved through the filtered set without repeating or dropping a ticket.
        Assert.Equal(3, Refs(page1).Concat(Refs(page2)).Distinct().Count());
    }

    [Fact]
    public async Task Filtering_IsDoneByTheDatabase_InOneCountQueryAndOnePageQuery()
    {
        await Run(new TicketQuery { Priority = TicketPriority.High, Search = "Fabrikam", PageSize = 2 });

        Assert.Equal(2, _commands.Statements.Count);

        var count = _commands.Statements[0];
        var page = _commands.Statements[1];

        Assert.Contains("COUNT(*)", count);
        Assert.Contains("WHERE", count);
        Assert.Contains("\"Priority\"", count);

        // The page query filters before it sorts and pages.
        Assert.Contains("WHERE", page);
        Assert.Contains("\"Priority\"", page);
        Assert.Contains("ORDER BY", page);
        Assert.Contains("LIMIT", page);
        Assert.True(page.IndexOf("WHERE", StringComparison.Ordinal) < page.IndexOf("ORDER BY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetEscalationsAsync_ReturnsTheNewestFirst_WithBeforeAndAfterValues()
    {
        // TCK-0004 is Medium and Alex's. Two escalations an hour apart: Ben takes it, then nobody
        // is eligible, so it ends up unassigned.
        var ticket = _db.Set<Ticket>().Single(t => t.Reference == "TCK-0004");
        var now = FixedClock.DefaultNow;

        var first = ticket.Escalate("Customer chased twice", "Priya Nair", now.AddHours(9), _benId, now.AddHours(1));
        var second = ticket.Escalate("Now blocking a launch", "Priya Nair", now.AddHours(6), null, now.AddHours(2));

        _db.AddRange(first, second);
        _db.SaveChanges();

        var history = await Queries().GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);

        // Newest first: the second escalation, High to Critical, Ben to nobody.
        Assert.Equal(TicketPriority.High, history[0].FromPriority);
        Assert.Equal(TicketPriority.Critical, history[0].ToPriority);
        Assert.Equal("Ben Osei", history[0].FromAgent?.FullName);
        Assert.Null(history[0].ToAgent);
        Assert.Equal(now.AddHours(9), history[0].FromDueAtUtc);
        Assert.Equal(now.AddHours(6), history[0].ToDueAtUtc);
        Assert.Equal("Now blocking a launch", history[0].Reason);
        Assert.Equal("Priya Nair", history[0].EscalatedBy);
        Assert.Equal(now.AddHours(2), history[0].EscalatedAtUtc);

        // Then the first one, Medium to High, Alex to Ben, from a ticket that had no due date.
        Assert.Equal(TicketPriority.Medium, history[1].FromPriority);
        Assert.Equal(TicketPriority.High, history[1].ToPriority);
        Assert.Equal("Alex Turner", history[1].FromAgent?.FullName);
        Assert.Equal("Ben Osei", history[1].ToAgent?.FullName);
        Assert.Null(history[1].FromDueAtUtc);
    }

    [Fact]
    public async Task GetEscalationsAsync_OnlyReturnsThatTicketsHistory()
    {
        var escalated = _db.Set<Ticket>().Single(t => t.Reference == "TCK-0008");
        var other = _db.Set<Ticket>().Single(t => t.Reference == "TCK-0003");

        _db.Add(escalated.Escalate("Customer chased twice", "Priya Nair", FixedClock.DefaultNow.AddHours(24), null, FixedClock.DefaultNow));
        _db.SaveChanges();

        var history = await Queries().GetEscalationsAsync(other.Id, CancellationToken.None);

        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetEscalationsAsync_ForATicketThatDoesNotExist_IsNull_NotAnEmptyList()
    {
        var history = await Queries().GetEscalationsAsync(9999, CancellationToken.None);

        Assert.Null(history);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task<PagedResult<TicketListItemDto>> Run(TicketQuery query) =>
        Queries().GetPagedAsync(query.Normalized(), CancellationToken.None);

    private TicketQueries Queries() => new(_db, new FixedClock(), TestSlaPolicy.Default());

    private static IEnumerable<string> Refs(PagedResult<TicketListItemDto> result) =>
        result.Items.Select(item => item.Reference);

    private void Seed(
        string reference,
        string title,
        Customer customer,
        Category category,
        TicketPriority priority,
        TicketStatus status,
        Agent? agent)
    {
        var now = FixedClock.DefaultNow;

        var ticket = Ticket.Raise(
            reference, title, "A description long enough to be realistic.", customer.Id, category.Id, priority, now);

        if (agent is not null)
        {
            ticket.AssignTo(agent.Id, now);
        }

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, now);
        }

        _db.Add(ticket);
    }

    /// <summary>Records the SQL text of every query the context runs.</summary>
    private sealed class CommandLog : DbCommandInterceptor
    {
        public List<string> Statements { get; } = [];

        public void Clear() => Statements.Clear();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Statements.Add(command.CommandText);

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
