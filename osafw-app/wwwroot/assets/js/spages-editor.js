/* Spages owns its block contract. Applications may register matching client tools and server renderers. */
(function () {
    'use strict';
    const form = document.getElementById('spages-editor');
    const actions = document.getElementById('cms-actions');
    document.querySelectorAll('select[data-selected]').forEach(el => { el.value = el.dataset.selected || el.options[0].value; });
    async function request(url, body) {
        const response = await fetch(url, { method: 'POST', body, credentials: 'same-origin', headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' } });
        const result = await response.json();
        if (!response.ok || result.success === false || result.error) {
            const error = new Error(result.error?.message || result.message || 'The change could not be saved. Please try again.');
            error.conflict = response.status === 409;
            throw error;
        }
        return result;
    }
    if (actions) document.querySelectorAll('[data-cms-action]').forEach(button => button.addEventListener('click', async () => {
        button.disabled = true;
        const output = document.getElementById('cms-action-result');
        try {
            const body = new FormData(actions);
            if (button.dataset.sample) body.set('sample', button.dataset.sample);
            await request(actions.dataset.base + '/(' + button.dataset.cmsAction + ')' , body);
            location.reload();
        } catch (error) { output.textContent = error.message; button.disabled = false; }
    }));
    if (!form) return;
    const state = document.getElementById('spages-save-state');
    const errorBox = document.getElementById('spages-error');
    const documentField = document.getElementById('spages-document');
    const layouts = JSON.parse(document.getElementById('spages-layouts').value);
    const snippets = JSON.parse(document.getElementById('spages-snippets').value);
    let doc = JSON.parse(documentField.value);
    let pageId = Number(form.dataset.id || 0);
    let version = Number(form.elements.expected_version.value || 0);
    let dirty = false, sequence = 0, saving = null, conflict = false, ready = false, timer;
    const editors = {};
    const mediaPickers = [];
    const base = form.dataset.base;
    function markChanged() {
        if (conflict) return;
        dirty = true; sequence++;
        state.textContent = 'Unsaved changes';
        clearTimeout(timer);
        timer = setTimeout(() => save(true).catch(showError), 1800);
    }
    function changed(event) {
        if (event?.target && ['spages-note', 'spages-publish-at'].includes(event.target.id)) return;
        if (!ready) return;
        markChanged();
    }
    function showError(error) {
        errorBox.hidden = false;
        errorBox.textContent = error.message;
        state.textContent = error.conflict ? 'Conflict — your input is still here. Reload to see the other changes, or copy your changes before reloading.' : 'Not saved';
        if (error.conflict) { conflict = true; clearTimeout(timer); }
    }
    function element(tag, className, text) {
        const el = document.createElement(tag);
        if (className) el.className = className;
        if (text) el.textContent = text;
        return el;
    }
    const tools = {};
    let helpId = 0;
    function field(container, name, label, value, options = {}) {
        const wrapper = element('label', 'spages-tool-field', label);
        const input = element(options.multiline ? 'textarea' : options.choices ? 'select' : 'input', options.choices ? 'form-select' : 'form-control');
        input.dataset.field = name;
        if (options.choices) options.choices.forEach(choice => {
            const option = element('option', '', choice[1]); option.value = choice[0]; input.append(option);
        });
        if (options.checkbox) { input.type = 'checkbox'; input.className = 'form-check-input ms-2'; input.checked = !!value; }
        else input.value = value ?? '';
        if (options.number) { input.type = 'number'; input.min = options.min ?? 0; }
        if (options.multiline) input.rows = 3;
        if (options.maxLength) input.maxLength = options.maxLength;
        if (options.pattern) input.pattern = options.pattern;
        if (options.placeholder) input.placeholder = options.placeholder;
        wrapper.append(input);
        if (options.help) {
            const help = element('span', 'form-text', options.help);
            help.id = 'spages-help-' + (++helpId);
            input.setAttribute('aria-describedby', help.id);
            wrapper.append(help);
        }
        container.append(wrapper);
        return input;
    }
    function linkLabelWarning(input, urlInput, optional) {
        const warning = element('span', 'form-text text-warning-emphasis');
        warning.id = 'spages-label-warning-' + (++helpId);
        warning.setAttribute('role', 'status');
        input.setAttribute('aria-describedby', [input.getAttribute('aria-describedby'), warning.id].filter(Boolean).join(' '));
        input.closest('label').append(warning);
        const vague = /^(click here|here|learn more|more|read more|link|website)$/i;
        function update() {
            const label = input.value.trim();
            const url = urlInput?.value.trim() || '';
            warning.textContent = '';
            if (!label && (!optional || url)) warning.textContent = 'Add a label that tells readers where this link goes.';
            else if (vague.test(label)) warning.textContent = 'Use a more specific label that describes the destination.';
            else if (label && (label === url || /^(?:https?:\/\/|mailto:|tel:|\/|#)/i.test(label))) warning.textContent = 'Describe the destination instead of using its URL as the label.';
            warning.hidden = !warning.textContent;
        }
        input.addEventListener('input', update);
        urlInput?.addEventListener('input', update);
        update();
    }
    function register(type, title, fields, defaults = {}, extra = null) {
        tools[type] = class {
            static get toolbox() { return { title, icon: '<svg width="18" height="18" viewBox="0 0 18 18"><path d="M3 3h12v12H3zM6 7h6M6 10h6" fill="none" stroke="currentColor"/></svg>' }; }
            static get isReadOnlySupported() { return true; }
            constructor({ data, readOnly }) { this.data = Object.assign({}, defaults, data); this.readOnly = readOnly; }
            render() {
                this.root = element('div', 'spages-tool');
                this.root.append(element('div', 'small fw-semibold text-body-secondary mb-2', title));
                fields.forEach(def => field(this.root, def[0], def[1], this.data[def[0]], def[2] || {}));
                fields.filter(def => def[2]?.linkLabel).forEach(def => linkLabelWarning(this.root.querySelector('[data-field="' + def[0] + '"]'), this.root.querySelector('[data-field="' + (def[2].linkUrlField || 'url') + '"]'), !!def[2].optional));
                if (extra) extra(this.root, this.data, this.readOnly);
                if (this.readOnly) this.root.querySelectorAll('input,textarea,select,button').forEach(el => { el.disabled = true; });
                return this.root;
            }
            save(root) {
                const data = Object.assign({}, this.data);
                root.querySelectorAll('[data-field]').forEach(input => { data[input.dataset.field] = input.type === 'checkbox' ? input.checked : input.type === 'number' ? Number(input.value) : input.value; });
                if (type === 'list') data.items = data.lines.split('\n').filter(line => line.trim());
                if (type === 'cards') data.items = Array.from(root.querySelectorAll('[data-card]')).map(card => Object.fromEntries(Array.from(card.querySelectorAll('[data-card-field]')).map(input => [input.dataset.cardField, input.value])));
                if (type === 'table') data.content = Array.from(root.querySelectorAll('tr')).map(row => Array.from(row.querySelectorAll('input')).map(input => input.value));
                return data;
            }
        };
    }
    register('header', 'Heading', [['text', 'Heading text'], ['level', 'Heading level', { choices: [[2, 'H2 — section'], [3, 'H3 — subsection'], [4, 'H4'], [5, 'H5'], [6, 'H6']] }], ['anchor', 'Link anchor (optional)', { maxLength: 64, pattern: '[A-Za-z][A-Za-z0-9_-]{0,63}', placeholder: 'section-name', help: 'Starts with a letter; use letters, numbers, hyphens, or underscores.' }]], { level: 2 });
    register('quote', 'Quote', [['text', 'Quotation', { multiline: true }], ['caption', 'Attribution']]);
    register('code', 'Code', [['code', 'Code (displayed as text)', { multiline: true }]]);
    register('delimiter', 'Divider', []);
    register('legacyMarkdown', 'Markdown', [['text', 'Markdown content', { multiline: true }]]);
    register('callout', 'Callout', [['title', 'Title'], ['text', 'Message', { multiline: true }]]);
    register('button', 'Button', [['text', 'Useful link label', { linkLabel: true }], ['url', 'Destination URL']]);
    register('snippet', 'Reusable snippet', [['key', 'Snippet', { choices: [['', 'Choose a published snippet'], ...snippets.map(x => [x.key, x.title])] }]]);
    register('list', 'List', [['style', 'Style', { choices: [['unordered', 'Bullets'], ['ordered', 'Numbered']] }]], { items: [], style: 'unordered' }, (root, data) => {
        field(root, 'lines', 'One item per line', data.items.map(x => typeof x === 'string' ? x : x.content).join('\n'), { multiline: true });
    });
    register('cards', 'Cards', [], { items: [] }, (root, data) => {
        function addCard(item = {}) {
            const card = element('fieldset', 'border rounded p-3 mb-2'); card.dataset.card = '';
            card.append(element('legend', 'h6', 'Card'));
            const inputs = {};
            ['title', 'text', 'url', 'label'].forEach(key => { const input = field(card, key, { title: 'Title', text: 'Description', url: 'Link URL (optional)', label: 'Useful link label' }[key], item[key]); delete input.dataset.field; input.dataset.cardField = key; inputs[key] = input; });
            linkLabelWarning(inputs.label, inputs.url, true);
            const remove = element('button', 'btn btn-sm btn-outline-danger mt-2', 'Remove card'); remove.type = 'button'; remove.onclick = () => { card.remove(); changed(); }; card.append(remove);
            root.insertBefore(card, add);
        }
        const add = element('button', 'btn btn-sm btn-default', 'Add card'); add.type = 'button'; add.onclick = () => { if (root.querySelectorAll('[data-card]').length < 12) { addCard(); changed(); } }; root.append(add);
        data.items.forEach(addCard);
    });
    register('table', 'Table', [['caption', 'Table caption'], ['withHeadings', 'First row contains column headings', { checkbox: true }]], { withHeadings: true, content: [['', ''], ['', '']] }, (root, data) => {
        const scroll = element('div', 'table-responsive'); const table = element('table', 'table table-sm'); scroll.append(table); root.append(scroll);
        function cell(row, value) { const td = element('td'); const input = element('input', 'form-control'); input.value = value; input.setAttribute('aria-label', 'Row ' + (row.rowIndex + 1) + ', column ' + (row.cells.length + 1)); td.append(input); row.append(td); }
        function row(values) { const tr = element('tr'); table.append(tr); values.forEach(value => cell(tr, value)); }
        data.content.forEach(row);
        const addRow = element('button', 'btn btn-sm btn-default me-2', 'Add row'); addRow.type = 'button'; addRow.onclick = () => { row(Array(table.rows[0]?.cells.length || 2).fill('')); changed(); };
        const addColumn = element('button', 'btn btn-sm btn-default', 'Add column'); addColumn.type = 'button'; addColumn.onclick = () => { Array.from(table.rows).forEach(tr => cell(tr, '')); changed(); }; root.append(addRow, addColumn);
    });
    function mediaPicker(root, data, image, options = {}) {
        const choices = [['', image ? 'No image selected' : 'No file selected']];
        if (data.att_id) choices.push([data.att_id, 'Current attachment #' + data.att_id]);
        const select = field(root, 'att_id', options.label || (image ? 'Image' : 'File'), data.att_id, { choices });
        select.disabled = !!options.readOnly;
        if (options.valueTarget) select.addEventListener('change', () => { options.valueTarget.value = select.value; });
        const note = element('p', 'small text-body-secondary', pageId ? 'Loading the file library…' : 'Choose a file now; the draft will be saved before upload.');
        root.append(note);
        const upload = field(root, '', options.uploadLabel || 'Upload a page-owned file', ''); delete upload.dataset.field; upload.type = 'file'; if (image) upload.accept = 'image/*'; upload.disabled = !!options.readOnly;
        let activated = false;
        async function activate() {
            if (activated || !pageId) return;
            activated = true;
            const selected = String(select.value || data.att_id || '');
            note.textContent = 'Loading the file library…';
            try {
                const response = await fetch(base + '/(Media)/' + pageId, { credentials: 'same-origin', headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' } });
                const result = await response.json();
                if (!response.ok || result.error) throw new Error(result.error?.message || 'The file library could not be loaded.');
                (result.items || []).filter(item => !image || item.is_image === true || Number(item.is_image) === 1).forEach(item => {
                    if (Array.from(select.options).some(option => option.value === String(item.id))) return;
                    const option = element('option', '', item.iname || item.fname || 'Attachment #' + item.id); option.value = item.id; select.append(option);
                });
                select.value = selected;
                note.className = 'small text-body-secondary';
                note.textContent = 'Choose an existing file or upload a new one.';
            } catch (error) { activated = false; note.className = 'text-danger small'; note.textContent = error.message; }
        }
        upload.addEventListener('change', async () => {
            if (options.readOnly || !upload.files.length) return;
            const file = upload.files[0];
            upload.disabled = true;
            try {
                if (!pageId) {
                    if (!form.reportValidity()) throw new Error('Complete the required page details before uploading this file. Your selection is still here.');
                    await Promise.resolve();
                    await save(true);
                }
                if (!pageId) throw new Error('Save the draft before uploading this file. Your selection is still here.');
                await activate();
                const payload = new FormData(); payload.set('XSS', form.elements.XSS.value); payload.set('file', file);
                const result = await request(base + '/(Upload)/' + pageId, payload);
                let option = Array.from(select.options).find(item => item.value === String(result.id));
                if (!option) { option = element('option', '', result.name); option.value = result.id; select.append(option); }
                select.value = result.id; markChanged();
                if (options.valueTarget) options.valueTarget.value = result.id;
                upload.value = '';
            } catch (error) { showError(error); } finally { upload.disabled = !!options.readOnly; }
        });
        const picker = { activate };
        mediaPickers.push(picker);
        void activate();
        return picker;
    }
    register('image', 'Image', [['alt', 'Alternative text'], ['decorative', 'Decorative — conveys no information', { checkbox: true }], ['caption', 'Caption (optional)']], { decorative: false }, (root, data, readOnly) => { mediaPicker(root, data, true, { readOnly }); });
    register('file', 'File', [['title', 'Descriptive file link label', { linkLabel: true }]], {}, (root, data, readOnly) => { mediaPicker(root, data, false, { readOnly }); });
    window.SpagesEditor = Object.assign(window.SpagesEditor || {}, { builtInTools: tools });
    const isSnippet = form.elements['item[is_snippet]'].value === '1';
    class UnavailableTool {
        static get isReadOnlySupported() { return true; }
        constructor({ data, readOnly }) { this.data = data; this.readOnly = readOnly; }
        render() {
            const root = element('div', 'spages-tool spages-tool-unavailable');
            const nestedSnippet = isSnippet && this.data.originalType === 'snippet';
            root.append(element('div', 'fw-semibold', nestedSnippet ? 'Nested reusable snippet' : 'Unavailable block: ' + (this.data.originalType || 'unknown')));
            root.append(element('p', 'small mb-2', nestedSnippet ? 'Reusable snippets cannot contain another snippet. Remove or replace this block; its original JSON remains available below.' : 'This site does not have the editor control for this block. Its original JSON is kept below and will be saved intact.'));
            this.input = field(root, 'raw', 'Original block JSON', this.data.raw, { multiline: true, help: 'Correct the JSON here or install the matching editor tool. Invalid JSON blocks saving so the original content cannot be lost.' });
            this.input.rows = 8;
            if (this.readOnly) this.input.disabled = true;
            return root;
        }
        save() { return { originalType: this.data.originalType, raw: this.input.value }; }
    }
    const configuredTools = Object.assign({}, tools, window.SpagesEditor.tools || {});
    if (isSnippet) delete configuredTools.snippet;
    const availableTools = new Set(['paragraph', ...Object.entries(configuredTools).filter(([, value]) => typeof value === 'function' || typeof value?.class === 'function').map(([name]) => name)]);
    let unavailableType = 'spagesUnavailable';
    while (Object.prototype.hasOwnProperty.call(configuredTools, unavailableType)) unavailableType = '_' + unavailableType;
    configuredTools[unavailableType] = { class: UnavailableTool, toolbox: false };
    function prepareRegion(region) {
        const prepared = Object.assign({}, region || { blocks: [] });
        prepared.blocks = Array.isArray(prepared.blocks) ? prepared.blocks.map(block => {
            if (block && availableTools.has(block.type)) return block;
            const originalType = typeof block?.type === 'string' ? block.type : 'unknown';
            return { type: unavailableType, data: { originalType, raw: JSON.stringify(block, null, 2) } };
        }) : [];
        return prepared;
    }
    function restoreUnavailable(region, regionName) {
        region.blocks = region.blocks.map(block => {
            if (block.type !== unavailableType) return block;
            let original;
            try { original = JSON.parse(block.data.raw); }
            catch (_) { throw new Error('The unavailable “' + block.data.originalType + '” block in ' + regionName + ' has invalid JSON. Correct its raw JSON before saving; your input is still here.'); }
            if (!original || Array.isArray(original) || typeof original !== 'object' || typeof original.type !== 'string' || !original.data || Array.isArray(original.data) || typeof original.data !== 'object')
                throw new Error('The unavailable “' + block.data.originalType + '” block in ' + regionName + ' must remain an object with a type and data object. Correct its raw JSON before saving; your input is still here.');
            return original;
        });
        return region;
    }
    const slot = document.getElementById('spages-slot');
    snippets.forEach(snippet => { const option = element('option', '', snippet.title); option.value = snippet.key; slot.append(option); });
    slot.value = doc.slots?.after_content || '';
    if (isSnippet) slot.closest('label').hidden = true;
    const snippetKey = form.elements['item[snippet_key]'];
    if (snippetKey && pageId) snippetKey.readOnly = true;
    const headMedia = document.getElementById('spages-head-media');
    if (headMedia) mediaPicker(headMedia, { att_id: Number(headMedia.dataset.attId || 0) }, true, { label: 'Page image', uploadLabel: 'Upload a page image', valueTarget: document.getElementById('spages-head-att') });
    function updateLayout() {
        const layout = layouts[document.getElementById('spages-layout').value];
        document.querySelectorAll('[data-region]').forEach(region => { region.hidden = !layout.regions.includes(region.dataset.region); });
        slot.closest('label').hidden = !layout.slots.includes('after_content') || isSnippet;
    }
    async function collect() {
        const labels = { main: 'page content', left: 'left column', right: 'right column' };
        for (const [region, editor] of Object.entries(editors)) doc.regions[region] = restoreUnavailable(await editor.save(), labels[region]);
        doc.slots = { ...doc.slots, after_content: slot.value };
        documentField.value = JSON.stringify(doc);
    }
    async function save(autosave = false) {
        if (conflict) throw Object.assign(new Error('Reload this page before saving again. Your unsaved input has been preserved.'), { conflict: true });
        if (saving) { await saving; if (autosave && !dirty) return; }
        if (autosave && (!dirty || !document.getElementById('spages-title').value.trim() || !form.checkValidity())) return;
        if (!autosave && !form.reportValidity()) throw new Error('Complete the required fields before saving.');
        const started = sequence;
        saving = (async () => {
            state.textContent = 'Saving draft…';
            await collect();
            const body = new FormData(form); body.set('expected_version', version); body.set('autosave', autosave ? '1' : '0');
            const result = await request(base + '/' + pageId, body);
            pageId = result.id; version = result.version; form.elements.expected_version.value = version;
            if (!Number(form.dataset.id)) { form.dataset.id = pageId; history.replaceState(null, '', result.location); }
            if (snippetKey && pageId) snippetKey.readOnly = true;
            await Promise.all(mediaPickers.map(picker => picker.activate()));
            dirty = sequence !== started;
            state.textContent = dirty ? 'Unsaved changes' : 'Draft saved at ' + new Date().toLocaleTimeString();
            errorBox.hidden = true;
        })();
        try { await saving; } finally { saving = null; }
    }
    form.addEventListener('submit', event => { event.preventDefault(); clearTimeout(timer); save(false).catch(showError); });
    form.addEventListener('input', changed);
    form.addEventListener('change', changed);
    document.getElementById('spages-layout').addEventListener('change', updateLayout);
    form.querySelector('[data-preview]').addEventListener('click', async () => {
        const preview = window.open('about:blank', '_blank');
        try { if (!pageId || dirty) await save(true); if (pageId && preview) { preview.opener = null; preview.location = base + '/(Preview)/' + pageId; } else preview?.close(); }
        catch (error) { preview?.close(); showError(error); }
    });
    form.querySelectorAll('[data-workflow]').forEach(button => button.addEventListener('click', async () => {
        button.disabled = true; clearTimeout(timer);
        try {
            if (dirty || !pageId) await save(false);
            const body = new FormData(); body.set('XSS', form.elements.XSS.value); body.set('expected_version', version); body.set('note', document.getElementById('spages-note').value);
            const date = document.getElementById('spages-publish-at')?.value;
            if (date) body.set('publish_at', new Date(date).toISOString());
            const result = await request(base + '/(' + button.dataset.workflow + ')/' + pageId, body);
            version = result.version; form.elements.expected_version.value = version; document.getElementById('spages-workflow').textContent = result.workflow;
            dirty = false; state.textContent = 'Publication settings saved'; errorBox.hidden = true; location.reload();
        } catch (error) { showError(error); } finally { button.disabled = false; }
    }));
    form.querySelectorAll('[data-restore]').forEach(button => button.addEventListener('click', async () => {
        try {
            if (dirty) await save(false);
            const body = new FormData(); body.set('XSS', form.elements.XSS.value); body.set('expected_version', version); body.set('revision_id', button.dataset.restore);
            await request(base + '/(RestoreRevision)/' + pageId, body); dirty = false; location.reload();
        } catch (error) { showError(error); }
    }));
    window.addEventListener('beforeunload', event => { if (dirty) { event.preventDefault(); event.returnValue = ''; } });
    if (!window.EditorJS) { showError(new Error('The editor assets could not be loaded. Restore the pinned assets with LibMan and reload.')); return; }
    for (const region of ['main', 'left', 'right']) editors[region] = new window.EditorJS({
        holder: 'spages-' + region, data: prepareRegion(doc.regions[region]), minHeight: region === 'main' ? 220 : 100,
        placeholder: 'Start writing, or choose + to add a block', tools: configuredTools, onChange: changed
    });
    Promise.all(Object.values(editors).map(editor => editor.isReady)).then(() => { ready = true; updateLayout(); }).catch(showError);
}());
