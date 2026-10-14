import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalationResult, TicketDetail } from '../../../types/api';
import {
  canEscalate,
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  validateEscalation,
  type EscalationErrors,
} from '../escalation';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (result: EscalationResult) => void;
}

/**
 * Raises the ticket's priority one level. Checks the same limits as the API before calling it,
 * and shows the API's own message when it refuses (for example a 409). When the ticket cannot be
 * escalated at all, it says why instead of showing the form. It does not ask who is escalating:
 * the history records the signed-in agent.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<EscalationErrors>({});
  const [isSaving, setIsSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string>();
  const [lastResult, setLastResult] = useState<EscalationResult>();

  const check = canEscalate(ticket);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validateEscalation({ reason });
    setErrors(validationErrors);
    setSubmitError(undefined);
    setLastResult(undefined);

    // Invalid input never reaches the API.
    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      // Only the reason goes up. The API records the signed-in agent from the token.
      const result = await ticketsApi.escalate(ticket.id, { reason: reason.trim() });

      setReason('');
      setLastResult(result);
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError && Object.keys(caught.fieldErrors).length > 0) {
        setErrors(fromApiErrors(caught.fieldErrors));
      }

      setSubmitError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="card">
      <h2>Escalation</h2>

      {lastResult && (
        <div className="notice escalation-result" role="status">
          <p>
            <strong>Escalated to {lastResult.escalation.toPriority}.</strong>
          </p>
          <p>{lastResult.slaReason}</p>
          <p>{lastResult.assignmentReason}</p>
        </div>
      )}

      {check.allowed ? (
        <form className="form" onSubmit={submit} noValidate>
          <Field
            id="escalation-reason"
            label="Reason"
            error={errors.reason}
            hint={`Why does this need to go up? ${REASON_MIN_LENGTH} to ${REASON_MAX_LENGTH} characters.`}
          >
            {(fieldProps) => (
              <textarea
                {...fieldProps}
                rows={3}
                value={reason}
                disabled={isSaving}
                onChange={(event) => setReason(event.target.value)}
              />
            )}
          </Field>

          {submitError && (
            <p className="field__error" role="alert">
              {submitError}
            </p>
          )}

          <div className="button-row">
            <button type="submit" className="button button--primary" disabled={isSaving}>
              {isSaving ? 'Escalating...' : `Escalate to ${check.nextPriority}`}
            </button>
          </div>
        </form>
      ) : (
        <p className="field__hint">{check.explanation}</p>
      )}
    </div>
  );
}

/** Picks the reason out of the API's per-field messages, whatever their casing. */
function fromApiErrors(fieldErrors: Record<string, string[]>): EscalationErrors {
  const first = (name: string) =>
    Object.entries(fieldErrors).find(([key]) => key.toLowerCase() === name)?.[1][0];

  return { reason: first('reason') };
}
