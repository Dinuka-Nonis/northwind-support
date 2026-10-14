import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { escalationResult, ticketDetail } from '../../../test/fixtures';
import type { EscalationResult, TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: { escalate: vi.fn() },
}));

const escalate = vi.mocked(ticketsApi.escalate);

/** Types into the form and presses the button for a High ticket (which goes to Critical). */
async function fillAndSubmit(reason: string, escalatedBy: string) {
  const user = userEvent.setup();

  await user.type(screen.getByLabelText('Reason'), reason);
  await user.type(screen.getByLabelText('Escalated by'), escalatedBy);
  await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));
}

describe('EscalationPanel', () => {
  beforeEach(() => {
    escalate.mockReset();
  });

  it('does not call the API when the reason is too short', async () => {
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fillAndSubmit('abc', 'Priya Nair');

    expect(escalate).not.toHaveBeenCalled();
    expect(screen.getByText(/at least 5 characters/i)).toBeInTheDocument();
  });

  it('does not call the API when nobody is named', async () => {
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fillAndSubmit('Customer chased twice', '   ');

    expect(escalate).not.toHaveBeenCalled();
    expect(screen.getByText('Say who is escalating this ticket.')).toBeInTheDocument();
  });

  it('sends the trimmed values, reports the result and shows how the SLA and owner were decided', async () => {
    const result = escalationResult();
    const onEscalated = vi.fn();
    escalate.mockResolvedValue(result);

    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillAndSubmit('  Customer chased twice  ', '  Priya Nair  ');

    expect(escalate).toHaveBeenCalledWith(12, {
      reason: 'Customer chased twice',
      escalatedBy: 'Priya Nair',
    });
    expect(onEscalated).toHaveBeenCalledWith(result);

    expect(await screen.findByText('Escalated to Critical.')).toBeInTheDocument();
    expect(screen.getByText(result.slaReason)).toBeInTheDocument();
    expect(screen.getByText(result.assignmentReason)).toBeInTheDocument();
  });

  it('shows a refusal from the API (409) and leaves the ticket alone', async () => {
    const onEscalated = vi.fn();
    escalate.mockRejectedValue(new ApiError(409, 'Ticket TCK-0012 is already Critical.'));

    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillAndSubmit('Customer chased twice', 'Priya Nair');

    expect(await screen.findByRole('alert')).toHaveTextContent('Ticket TCK-0012 is already Critical.');
    expect(onEscalated).not.toHaveBeenCalled();

    // The form is still there to try again.
    expect(screen.getByRole('button', { name: 'Escalate to Critical' })).toBeEnabled();
  });

  it('is busy while the request is in flight', async () => {
    escalate.mockReturnValue(new Promise<EscalationResult>(() => {}));

    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fillAndSubmit('Customer chased twice', 'Priya Nair');

    expect(await screen.findByRole('button', { name: 'Escalating...' })).toBeDisabled();
  });

  it.each<[string, Partial<TicketDetail>, RegExp]>([
    ['is already Critical', { priority: 'Critical' }, /already Critical/],
    ['is Resolved', { status: 'Resolved' }, /is resolved/],
    ['is Closed', { status: 'Closed' }, /is closed/],
    ['is Critical and Resolved', { priority: 'Critical', status: 'Resolved' }, /is resolved/],
  ])('explains instead of showing the form when the ticket %s', (_name, overrides, message) => {
    render(<EscalationPanel ticket={ticketDetail(overrides)} onEscalated={vi.fn()} />);

    expect(screen.getByText(message)).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Reason')).not.toBeInTheDocument();
  });
});
