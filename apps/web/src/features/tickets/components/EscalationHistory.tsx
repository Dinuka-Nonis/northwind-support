import { EmptyState } from '../../../components/EmptyState';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { AgentSummary, TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationHistoryProps {
  escalations?: TicketEscalation[];
  isLoading: boolean;
  error?: string;
  onRetry: () => void;
}

/** A ticket's escalations, newest first: what changed, why, who did it and when. */
export function EscalationHistory({ escalations, isLoading, error, onRetry }: EscalationHistoryProps) {
  return (
    <div className="card">
      <h2>Escalation history</h2>

      {error && <ErrorMessage message={error} onRetry={onRetry} />}

      {isLoading && !escalations && <Spinner label="Loading escalation history" />}

      {escalations &&
        (escalations.length === 0 ? (
          <EmptyState
            title="No escalations yet"
            description="Escalations of this ticket will be listed here, newest first."
          />
        ) : (
          <ol className="history">
            {escalations.map((escalation) => (
              <li key={escalation.id} className="history__item">
                <dl className="history__changes">
                  <div>
                    <dt>Priority</dt>
                    <dd>
                      <PriorityBadge priority={escalation.fromPriority} />
                      <Arrow />
                      <PriorityBadge priority={escalation.toPriority} />
                    </dd>
                  </div>

                  <div>
                    <dt>Agent</dt>
                    <dd>
                      <span>{agentName(escalation.fromAgent)}</span>
                      <Arrow />
                      <span>{agentName(escalation.toAgent)}</span>
                    </dd>
                  </div>

                  <div>
                    <dt>Due</dt>
                    <dd>
                      <span>{formatDateTime(escalation.fromDueAtUtc)}</span>
                      <Arrow />
                      <span>{formatDateTime(escalation.toDueAtUtc)}</span>
                    </dd>
                  </div>
                </dl>

                <p className="history__reason">{escalation.reason}</p>
                <p className="history__meta">
                  {escalation.escalatedBy} &middot; {formatDateTime(escalation.escalatedAtUtc)}
                </p>
              </li>
            ))}
          </ol>
        ))}
    </div>
  );
}

function agentName(agent: AgentSummary | null): string {
  return agent?.fullName ?? 'Unassigned';
}

/** An arrow for sighted users and the word "to" for screen readers. */
function Arrow() {
  return (
    <>
      <span aria-hidden="true">&rarr;</span>
      <span className="visually-hidden"> to </span>
    </>
  );
}
