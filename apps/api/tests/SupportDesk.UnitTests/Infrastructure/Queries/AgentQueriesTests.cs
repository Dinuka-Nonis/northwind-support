using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The agent list the assignment rules read, run against a real (in-memory SQLite) database so
/// that the open count and the number of statements are those of the SQL EF Core generates.
/// </summary>
public sealed class AgentQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly CommandLog _commands = new();
    private readonly SupportDbContext _db;
    private readonly int _alexId;
    private readonly int _benId;
    private readonly int _caraId;
    private readonly int _billingId;

    public AgentQueriesTests()
    {
        _connection.Open();

        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_commands)
            .Options);
        _db.Database.EnsureCreated();

        var now = FixedClock.DefaultNow;

        var customer = new Customer("Fabrikam", "ops@fabrikam.test", null, CustomerTier.Standard, now);
        var billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        var general = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        _db.AddRange(customer, billing, general);
        _db.SaveChanges();

        _billingId = billing.Id;

        // Alex has no specialization, Ben is a billing specialist, Cara is inactive.
        var alex = new Agent("Alex Turner", "alex@support.test", 3, now);
        var ben = new Agent("Ben Osei", "ben@support.test", 5, now);
        var cara = new Agent("Cara Diaz", "cara@support.test", 2, now);

        ben.AddSpecialization(billing.Id);
        cara.Deactivate();

        _db.AddRange(alex, ben, cara);
        _db.SaveChanges();

        _alexId = alex.Id;
        _benId = ben.Id;
        _caraId = cara.Id;

        // Alex holds two open tickets and two finished ones; only the open ones count.
        Seed("TCK-0001", customer, general, alex, TicketStatus.New);
        Seed("TCK-0002", customer, general, alex, TicketStatus.InProgress);
        Seed("TCK-0003", customer, general, alex, TicketStatus.Resolved);
        Seed("TCK-0004", customer, general, alex, TicketStatus.Closed);
        Seed("TCK-0005", customer, general, agent: null, TicketStatus.Open);

        _db.SaveChanges();

        // Seeding is not what the tests measure.
        _commands.Clear();
    }

    [Fact]
    public async Task GetAssignmentCandidatesAsync_CountsOnlyOpenTickets()
    {
        var candidates = await Run();

        var alex = Assert.Single(candidates, c => c.Id == _alexId);
        Assert.Equal(2, alex.OpenTickets);
        Assert.Equal(3, alex.MaxOpenTickets);
        Assert.True(alex.HasCapacity);

        Assert.Equal(0, Assert.Single(candidates, c => c.Id == _benId).OpenTickets);
    }

    [Fact]
    public async Task GetAssignmentCandidatesAsync_CarriesActivityAndSpecializations()
    {
        var candidates = await Run();

        var alex = Assert.Single(candidates, c => c.Id == _alexId);
        var ben = Assert.Single(candidates, c => c.Id == _benId);
        var cara = Assert.Single(candidates, c => c.Id == _caraId);

        Assert.Equal("Ben Osei", ben.Name);
        Assert.True(ben.IsQualifiedFor(_billingId));
        Assert.False(alex.IsQualifiedFor(_billingId));
        Assert.True(alex.IsActive);
        Assert.False(cara.IsActive);
    }

    [Fact]
    public async Task GetAssignmentCandidatesAsync_ListsEveryAgent_InIdOrder()
    {
        var candidates = await Run();

        Assert.Equal(new[] { _alexId, _benId, _caraId }, candidates.Select(c => c.Id));
    }

    [Fact]
    public async Task GetAssignmentCandidatesAsync_UsesOneStatement_NoMatterHowManyAgents()
    {
        await Run();

        Assert.Single(_commands.Statements);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task<IReadOnlyList<AgentCandidate>> Run() =>
        new AgentQueries(_db).GetAssignmentCandidatesAsync(CancellationToken.None);

    private void Seed(string reference, Customer customer, Category category, Agent? agent, TicketStatus status)
    {
        var now = FixedClock.DefaultNow;

        var ticket = Ticket.Raise(
            reference, "Something is not working", "A description long enough to be realistic.", customer.Id, category.Id, TicketPriority.Medium, now);

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
