import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

interface SlaCellProps {
  dueAtUtc: string | null;
  slaStatus: SlaStatus;
}

/** The SLA status and the due date, as shown in the ticket list and on the ticket page. */
export function SlaCell({ dueAtUtc, slaStatus }: SlaCellProps) {
  return (
    <div className="sla-cell">
      <SlaIndicator status={slaStatus} />
      <span className="sla-cell__due">{formatDateTime(dueAtUtc)}</span>
    </div>
  );
}
