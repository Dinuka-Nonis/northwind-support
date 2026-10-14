import { describe, expect, it } from 'vitest';
import type { TicketPriority, TicketStatus } from '../../types/api';
import { canEscalate, nextPriority, validateEscalation } from './escalation';

const valid = { reason: 'Customer chased twice', escalatedBy: 'Priya Nair' };

describe('validateEscalation', () => {
  it('accepts a reason and a name', () => {
    expect(validateEscalation(valid)).toEqual({});
  });

  it.each<[number, boolean]>([
    [4, true],
    [5, false],
    [500, false],
    [501, true],
  ])('a reason of %i characters is rejected: %s', (length, rejected) => {
    const errors = validateEscalation({ ...valid, reason: 'x'.repeat(length) });

    expect('reason' in errors).toBe(rejected);
  });

  it('measures the reason after trimming', () => {
    expect(validateEscalation({ ...valid, reason: '   ab   ' }).reason).toBeDefined();
    expect(validateEscalation({ ...valid, reason: '          ' }).reason).toBeDefined();
    expect(validateEscalation({ ...valid, reason: `  ${'x'.repeat(500)}  ` }).reason).toBeUndefined();
  });

  it.each<[string, boolean]>([
    ['', true],
    ['   ', true],
    ['x'.repeat(100), false],
    ['x'.repeat(101), true],
  ])('a name of %j is rejected: %s', (name, rejected) => {
    const errors = validateEscalation({ ...valid, escalatedBy: name });

    expect('escalatedBy' in errors).toBe(rejected);
  });
});

describe('nextPriority', () => {
  it.each<[TicketPriority, TicketPriority | undefined]>([
    ['Low', 'Medium'],
    ['Medium', 'High'],
    ['High', 'Critical'],
    ['Critical', undefined],
  ])('%s goes to %s', (priority, expected) => {
    expect(nextPriority(priority)).toBe(expected);
  });
});

describe('canEscalate', () => {
  it('allows an open ticket and names the next priority', () => {
    expect(canEscalate({ status: 'InProgress', priority: 'Medium' })).toEqual({
      allowed: true,
      nextPriority: 'High',
    });
  });

  it('refuses a Critical ticket', () => {
    const check = canEscalate({ status: 'Open', priority: 'Critical' });

    expect(check.allowed).toBe(false);
  });

  it.each<TicketStatus>(['Resolved', 'Closed'])('refuses a %s ticket', (status) => {
    expect(canEscalate({ status, priority: 'Low' }).allowed).toBe(false);
  });

  it('names the status first when the ticket is both Critical and Resolved, as the API does', () => {
    const check = canEscalate({ status: 'Resolved', priority: 'Critical' });

    expect(check).toMatchObject({ allowed: false, explanation: expect.stringContaining('resolved') });
  });
});
