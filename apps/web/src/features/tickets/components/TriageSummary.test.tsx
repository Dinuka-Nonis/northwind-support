import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { TriageResult } from '../../../types/api';
import { TriageSummary } from './TriageSummary';

function triage(overrides: Partial<TriageResult> = {}): TriageResult {
  return {
    priority: 'Critical',
    priorityReason: 'Critical: this category always forces Critical priority (Low was requested).',
    dueAtUtc: '2026-03-02T11:00:00Z',
    slaWindowHours: 2,
    slaReason: 'Critical priority: 4 h base window x 0.5 for a Premium customer = 2 h.',
    assignedAgent: { id: 3, fullName: 'Priya Nair' },
    assignmentReason: 'Assigned to Priya Nair: fewest open tickets (4) among 1 eligible specialist.',
    ...overrides,
  };
}

describe('TriageSummary', () => {
  it('shows the priority, the due date and the agent, each with its reason', () => {
    const result = triage();

    render(<TriageSummary triage={result} />);

    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText(result.priorityReason)).toBeInTheDocument();
    expect(screen.getByText(result.slaReason)).toBeInTheDocument();
    expect(screen.getByText('Priya Nair')).toBeInTheDocument();
    expect(screen.getByText(result.assignmentReason)).toBeInTheDocument();
  });

  it('says "Unassigned" and why when nobody could take the ticket', () => {
    const result = triage({
      assignedAgent: null,
      assignmentReason: 'Left unassigned: no active specialist with spare capacity for this category.',
    });

    render(<TriageSummary triage={result} />);

    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText(/no active specialist with spare capacity/)).toBeInTheDocument();
  });
});
