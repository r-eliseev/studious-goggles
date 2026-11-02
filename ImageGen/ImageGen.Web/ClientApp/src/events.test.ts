import { describe, expect, it } from 'vitest';
import { mergeGenerationEvent } from './events';

describe('mergeGenerationEvent', () => {
  it('updates one generation and ignores duplicate events', () => {
    const current = [{ id: 'g1', sessionId: 's1', status: 'Queued' }, { id: 'g2', sessionId: 's1', status: 'Running' }];
    const event = { generationId: 'g1', sessionId: 's1', status: 'Succeeded' };

    const next = mergeGenerationEvent(current, event, 's1');

    expect(next).toEqual([{ ...current[0], status: 'Succeeded' }, current[1]]);
    expect(mergeGenerationEvent(next, event, 's1')).toEqual(next);
  });

  it('does not apply an event for another session or an unknown generation', () => {
    const current = [{ id: 'g1', sessionId: 's1', status: 'Queued' }];

    expect(mergeGenerationEvent(current, { generationId: 'g1', sessionId: 's2', status: 'Failed' }, 's1')).toBe(current);
    expect(mergeGenerationEvent(current, { generationId: 'unknown', sessionId: 's1', status: 'Failed' }, 's1')).toBe(current);
  });
});
