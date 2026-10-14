import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

const cases: { status: SlaStatus; text: string; tone: string }[] = [
  { status: 'NotApplicable', text: 'No SLA', tone: 'neutral' },
  { status: 'WithinSla', text: 'Within SLA', tone: 'info' },
  { status: 'AtRisk', text: 'At risk', tone: 'warning' },
  { status: 'Breached', text: 'Breached', tone: 'danger' },
  { status: 'Met', text: 'Met', tone: 'success' },
];

describe('SlaIndicator', () => {
  it.each(cases)('shows $status as "$text" in the $tone tone', ({ status, text, tone }) => {
    const { container } = render(<SlaIndicator status={status} />);

    expect(screen.getByText(text)).toBeInTheDocument();
    expect(container.querySelector(`.badge--${tone}`)).not.toBeNull();
  });

  it('gives every status its own text and its own symbol', () => {
    const texts = new Set<string>();
    const glyphs = new Set<string>();

    for (const { status } of cases) {
      const { container, unmount } = render(<SlaIndicator status={status} />);
      const glyph = container.querySelector('.sla-indicator__glyph');

      expect(glyph).not.toBeNull();
      expect(glyph?.textContent).toBeTruthy();

      glyphs.add(glyph?.textContent ?? '');
      texts.add(container.textContent ?? '');
      unmount();
    }

    expect(texts.size).toBe(cases.length);
    expect(glyphs.size).toBe(cases.length);
  });

  it('hides the symbol from screen readers, which get the words', () => {
    const { container } = render(<SlaIndicator status="AtRisk" />);

    expect(container.querySelector('.sla-indicator__glyph')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.getByText('At risk')).not.toHaveAttribute('aria-hidden');
  });

  it('shows a status it does not know in plain words instead of failing', () => {
    render(<SlaIndicator status={'OnHold' as SlaStatus} />);

    expect(screen.getByText('On hold')).toBeInTheDocument();
  });
});
