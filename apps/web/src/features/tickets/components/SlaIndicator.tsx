import { Badge } from '../../../components/Badge';
import { humanize } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';
import { slaLabels, type SlaLabel } from './slaLabels';

/**
 * Where a ticket stands against its SLA: a coloured badge with a symbol and the status in
 * words. The words carry the meaning and the symbol backs up the colour, so it reads the same
 * for someone who cannot tell the colours apart.
 */
export function SlaIndicator({ status }: { status: SlaStatus }) {
  const label = slaLabels[status] ?? unknownLabel(status);

  return (
    <Badge tone={label.tone}>
      <span className="sla-indicator__glyph" aria-hidden="true">
        {label.glyph}
      </span>
      <span>{label.text}</span>
    </Badge>
  );
}

/** A status the web app has not heard of (the API grew one): show it plainly, not nothing. */
function unknownLabel(status: string): SlaLabel {
  return { text: humanize(status), tone: 'neutral', glyph: '?' };
}
