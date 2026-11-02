import { mount } from '@vue/test-utils';
import { describe, expect, it, vi } from 'vitest';
import ProviderKeysView from './ProviderKeysView.vue';

describe('ProviderKeysView', () => {
  it('clears a submitted key without persisting it in browser storage', async () => {
    const saveKey = vi.fn().mockResolvedValue(undefined);
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    const wrapper = mount(ProviderKeysView, { props: { providers: [{ id: 'openrouter', displayName: 'OpenRouter', kind: 'Image', maskedKey: null }], saveKey } });
    const input = wrapper.get('input[type="password"]');
    await input.setValue('secret-provider-key');
    await wrapper.get('form').trigger('submit');

    expect(saveKey).toHaveBeenCalledWith('openrouter', 'secret-provider-key');
    expect((input.element as HTMLInputElement).value).toBe('');
    expect(setItem).not.toHaveBeenCalled();
  });
});
