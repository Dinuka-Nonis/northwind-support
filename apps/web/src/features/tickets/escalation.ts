import { humanize } from '../../lib/format';
import type { TicketDetail, TicketPriority } from '../../types/api';

/** The same limits as the API's escalation validator, measured after trimming. */
export const REASON_MIN_LENGTH = 5;
export const REASON_MAX_LENGTH = 500;

const priorityOrder: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

/** The priority one level up, or undefined when the ticket is already Critical. */
export function nextPriority(priority: TicketPriority): TicketPriority | undefined {
  return priorityOrder[priorityOrder.indexOf(priority) + 1];
}

export interface EscalationInput {
  reason: string;
}

export type EscalationErrors = Partial<Record<keyof EscalationInput, string>>;

/** Checks the form against the API's limits. An empty result means it can be sent. */
export function validateEscalation(input: EscalationInput): EscalationErrors {
  const errors: EscalationErrors = {};
  const reasonLength = input.reason.trim().length;

  if (reasonLength < REASON_MIN_LENGTH) {
    errors.reason = `Give a reason of at least ${REASON_MIN_LENGTH} characters.`;
  } else if (reasonLength > REASON_MAX_LENGTH) {
    errors.reason = `The reason must be at most ${REASON_MAX_LENGTH} characters.`;
  }

  return errors;
}

export type EscalationCheck =
  | { allowed: true; nextPriority: TicketPriority }
  | { allowed: false; explanation: string };

/**
 * Whether a ticket can be escalated, and if not, why. The same order as the API, so the screen
 * and the 409 name the same reason: Resolved or Closed first, then already Critical.
 */
export function canEscalate(ticket: Pick<TicketDetail, 'status' | 'priority'>): EscalationCheck {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return {
      allowed: false,
      explanation: `This ticket is ${humanize(ticket.status).toLowerCase()}, so it cannot be escalated.`,
    };
  }

  const next = nextPriority(ticket.priority);

  if (next === undefined) {
    return {
      allowed: false,
      explanation: 'This ticket is already Critical, the highest priority, so it cannot be escalated further.',
    };
  }

  return { allowed: true, nextPriority: next };
}
