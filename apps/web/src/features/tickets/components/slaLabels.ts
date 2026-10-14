import type { BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

export interface SlaLabel {
  text: string;
  tone: BadgeTone;
  /** A symbol that differs for every status, so the state never depends on colour alone. */
  glyph: string;
}

/** How each SLA status is worded and drawn. The one place to change it. */
export const slaLabels: Record<SlaStatus, SlaLabel> = {
  NotApplicable: { text: 'No SLA', tone: 'neutral', glyph: '–' },
  WithinSla: { text: 'Within SLA', tone: 'info', glyph: '●' },
  AtRisk: { text: 'At risk', tone: 'warning', glyph: '▲' },
  Breached: { text: 'Breached', tone: 'danger', glyph: '✕' },
  Met: { text: 'Met', tone: 'success', glyph: '✓' },
};
