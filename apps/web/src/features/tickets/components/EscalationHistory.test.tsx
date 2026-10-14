import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { escalationRow } from '../../../test/fixtures';
import { EscalationHistory } from './EscalationHistory';

describe('EscalationHistory', () => {
  it('shows a loading state while the first load runs', () => {
    render(<EscalationHistory isLoading onRetry={vi.fn()} />);

    expect(screen.getByRole('status')).toHaveTextContent('Loading escalation history');
  });

  it('shows the error and lets the user try again', async () => {
    const onRetry = vi.fn();
    render(<EscalationHistory isLoading={false} error="Unable to load the escalation history." onRetry={onRetry} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Unable to load the escalation history.');

    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('says so when the ticket has never been escalated', () => {
    render(<EscalationHistory escalations={[]} isLoading={false} onRetry={vi.fn()} />);

    expect(screen.getByText('No escalations yet')).toBeInTheDocument();
  });

  it('lists each escalation in the order given, with what changed, why, who and when', () => {
    const newest = escalationRow({
      id: 2,
      fromPriority: 'High',
      toPriority: 'Critical',
      fromDueAtUtc: null,
      fromAgent: { id: 2, fullName: 'Ben Osei' },
      toAgent: null,
      reason: 'Now blocking a launch',
      escalatedBy: 'Sam Reyes',
    });

    render(<EscalationHistory escalations={[newest, escalationRow()]} isLoading={false} onRetry={vi.fn()} />);

    const [first, second] = screen.getAllByRole('listitem');

    expect(within(first).getByText('Now blocking a launch')).toBeInTheDocument();
    expect(within(first).getByText(/Sam Reyes/)).toBeInTheDocument();
    expect(within(first).getByText('Ben Osei')).toBeInTheDocument();

    // No owner after the escalation, and no due date before it.
    expect(within(first).getByText('Unassigned')).toBeInTheDocument();
    expect(within(first).getByText('-')).toBeInTheDocument();

    expect(within(second).getByText('Customer chased twice')).toBeInTheDocument();
    expect(within(second).getByText('Alex Turner')).toBeInTheDocument();
    expect(within(second).getByText('Ben Osei')).toBeInTheDocument();
    expect(within(second).getByText('Medium')).toBeInTheDocument();
    expect(within(second).getByText('High')).toBeInTheDocument();
  });
});
