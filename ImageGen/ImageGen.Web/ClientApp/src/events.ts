export type Generation = { id: string; sessionId: string; status: string; [key: string]: unknown };
export type GenerationEvent = { generationId: string; sessionId: string; status: string };

export function mergeGenerationEvent<T extends Generation>(generations: T[], event: GenerationEvent, sessionId: string): T[] {
  if (event.sessionId !== sessionId) return generations;
  const index = generations.findIndex(item => item.id === event.generationId && item.sessionId === sessionId);
  if (index < 0 || generations[index]?.status === event.status) return generations;
  const next = generations.slice();
  next[index] = { ...next[index]!, status: event.status };
  return next;
}

export function subscribeSessionEvents(
  sessionId: string,
  onGeneration: (event: GenerationEvent) => void,
  onResync: () => void,
  createSource: (url: string) => EventSource = url => new EventSource(url),
): () => void {
  const source = createSource('/api/events');
  let reconnecting = false;
  source.addEventListener('generation', raw => {
    const event = JSON.parse((raw as MessageEvent<string>).data) as GenerationEvent;
    if (event.sessionId === sessionId) onGeneration(event);
  });
  source.addEventListener('resync', () => onResync());
  source.onerror = () => {
    if (!reconnecting) {
      reconnecting = true;
      onResync();
    }
  };
  source.onopen = () => { reconnecting = false; };
  return () => source.close();
}
