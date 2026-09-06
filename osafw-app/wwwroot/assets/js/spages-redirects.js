(function () {
    'use strict';
    const form = document.getElementById('cms-redirect');
    if (!form) return;
    async function save(url, data, trigger) {
        const output = document.getElementById('redirect-result');
        if (trigger) { trigger.disabled = true; trigger.setAttribute('aria-busy', 'true'); }
        output.textContent = 'Saving…';
        try {
            const response = await fetch(url, { method: 'POST', body: data, headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' } });
            const result = await response.json();
            if (!response.ok || result.error) throw new Error(result.error?.message || 'The redirect could not be saved.');
            location.reload();
        } catch (error) {
            output.textContent = error.message;
            if (trigger) { trigger.disabled = false; trigger.removeAttribute('aria-busy'); }
        }
    }
    form.addEventListener('submit', event => { event.preventDefault(); save(form.action, new FormData(form), event.submitter); });
    document.querySelectorAll('[data-remove]').forEach(button => button.addEventListener('click', () => { const body = new FormData(); body.set('XSS', form.elements.XSS.value); save(button.dataset.remove, body, button); }));
}());
