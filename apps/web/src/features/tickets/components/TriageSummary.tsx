import { formatDateTime } from '../../../lib/format';
import type { TriageResult } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

/**
 * What the system decided for a new ticket, and why: its priority, its due date and its owner.
 * When nobody could take the ticket it says so, with the reason, instead of leaving a blank.
 */
export function TriageSummary({ triage }: { triage: TriageResult }) {
  return (
    <section className="triage" aria-labelledby="triage-heading">
      <h2 id="triage-heading">How it was triaged</h2>

      <dl className="summary">
        <div>
          <dt>Priority</dt>
          <dd>
            <PriorityBadge priority={triage.priority} />
            <span className="summary__note">{triage.priorityReason}</span>
          </dd>
        </div>

        <div>
          <dt>Due</dt>
          <dd>
            {formatDateTime(triage.dueAtUtc)}
            <span className="summary__note">{triage.slaReason}</span>
          </dd>
        </div>

        <div>
          <dt>Assigned to</dt>
          <dd>
            {triage.assignedAgent?.fullName ?? 'Unassigned'}
            <span className="summary__note">{triage.assignmentReason}</span>
          </dd>
        </div>
      </dl>
    </section>
  );
}
