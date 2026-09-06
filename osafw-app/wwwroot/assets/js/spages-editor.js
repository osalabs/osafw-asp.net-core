/* Spages owns its block contract. Applications may register matching client tools and server renderers. */
(function () {
    'use strict';

    const FORM = document.getElementById('spages-editor');
    const ACTIONS_FORM = document.getElementById('cms-actions');
    const WORKFLOW_IN_REVIEW = 10;

    document.querySelectorAll('select[data-selected]').forEach(SELECT => {
        SELECT.value = SELECT.dataset.selected || SELECT.options[0].value;
    });

    async function request(url, body) {
        const RESPONSE = await fetch(url, {
            method: 'POST',
            body,
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
        });
        const RESULT = await RESPONSE.json();
        if (!RESPONSE.ok || RESULT.success === false || RESULT.error) {
            throw new Error(RESULT.error?.message || RESULT.message || 'The change could not be saved. Please try again.');
        }
        return RESULT;
    }

    if (ACTIONS_FORM) {
        document.querySelectorAll('[data-cms-action]').forEach(BUTTON => {
            BUTTON.addEventListener('click', async () => {
                const OUTPUT = document.getElementById('cms-action-result');
                BUTTON.disabled = true;
                try {
                    await request(ACTIONS_FORM.dataset.base + '/(' + BUTTON.dataset.cmsAction + ')', new FormData(ACTIONS_FORM));
                    location.reload();
                } catch (error) {
                    OUTPUT.textContent = error.message;
                    BUTTON.disabled = false;
                }
            });
        });
    }

    if (!FORM) return;

    const STATE = document.getElementById('spages-save-state');
    const ERROR_BOX = document.getElementById('spages-error');
    const DOCUMENT_FIELD = document.getElementById('spages-document');
    const LAYOUTS = JSON.parse(document.getElementById('spages-layouts').value);
    const SNIPPETS = JSON.parse(document.getElementById('spages-snippets').value);
    const EDITORS = {};
    const MEDIA_PICKERS = [];
    const BASE_URL = FORM.dataset.base;
    const IS_SNIPPET = FORM.elements['item[is_snippet]'].value === '1';
    const IS_AUTHOR = isTrue(FORM.dataset.isAuthor);
    const URL_INPUT = document.getElementById('spages-url');
    const TOOLS = {};
    let pageId = Number(FORM.dataset.id || 0);
    let documentData = JSON.parse(DOCUMENT_FIELD.value);
    let isDirty = false;
    let isReady = false;
    let changeSequence = 0;
    let savePromise = null;
    let autosaveTimer;
    let helpId = 0;

    if (!IS_AUTHOR) {
        FORM.querySelectorAll('input, textarea, select, button').forEach(CONTROL => {
            if (!CONTROL.matches('[data-preview]')) CONTROL.disabled = true;
        });
        STATE.textContent = 'Read-only';
    }

    function isTrue(value) {
        return value === true || value === 1 || ['1', 'true'].includes(String(value).toLowerCase());
    }

    function markChanged() {
        isDirty = true;
        changeSequence++;
        STATE.textContent = 'Unsaved changes';
        clearTimeout(autosaveTimer);
        autosaveTimer = setTimeout(() => saveDraft(true).catch(showError), 1800);
    }

    function changed(event) {
        const IS_WORKFLOW_FIELD = event?.target && ['spages-note', 'spages-publish-at'].includes(event.target.id);
        if (isReady && !IS_WORKFLOW_FIELD) markChanged();
    }

    function showError(error) {
        ERROR_BOX.hidden = false;
        ERROR_BOX.textContent = error.message;
        STATE.textContent = 'Not saved — your input is still here.';
    }

    function element(tag, className, text) {
        const ELEMENT = document.createElement(tag);
        if (className) ELEMENT.className = className;
        if (text) ELEMENT.textContent = text;
        return ELEMENT;
    }

    function field(container, name, label, value, options = {}) {
        const WRAPPER = element('label', 'spages-tool-field', label);
        const INPUT = element(options.multiline ? 'textarea' : options.choices ? 'select' : 'input', options.choices ? 'form-select' : 'form-control');
        INPUT.dataset.field = name;
        (options.choices || []).forEach(CHOICE => {
            const OPTION = element('option', '', CHOICE[1]);
            OPTION.value = CHOICE[0];
            INPUT.append(OPTION);
        });
        if (options.checkbox) {
            INPUT.type = 'checkbox';
            INPUT.className = 'form-check-input ms-2';
            INPUT.checked = !!value;
        } else {
            INPUT.value = value ?? '';
        }
        if (options.number) {
            INPUT.type = 'number';
            INPUT.min = options.min ?? 0;
        }
        if (options.multiline) INPUT.rows = 3;
        if (options.maxLength) INPUT.maxLength = options.maxLength;
        if (options.pattern) INPUT.pattern = options.pattern;
        if (options.placeholder) INPUT.placeholder = options.placeholder;
        WRAPPER.append(INPUT);
        if (options.help) {
            const HELP = element('span', 'form-text', options.help);
            HELP.id = 'spages-help-' + (++helpId);
            INPUT.setAttribute('aria-describedby', HELP.id);
            WRAPPER.append(HELP);
        }
        container.append(WRAPPER);
        return INPUT;
    }

    function linkLabelWarning(input, urlInput, isOptional) {
        const WARNING = element('span', 'form-text text-warning-emphasis');
        const VAGUE_LABEL = /^(click here|here|learn more|more|read more|link|website)$/i;
        WARNING.id = 'spages-label-warning-' + (++helpId);
        WARNING.setAttribute('role', 'status');
        input.setAttribute('aria-describedby', [input.getAttribute('aria-describedby'), WARNING.id].filter(Boolean).join(' '));
        input.closest('label').append(WARNING);
        function updateWarning() {
            const LABEL = input.value.trim();
            const URL = urlInput?.value.trim() || '';
            WARNING.textContent = '';
            if (!LABEL && (!isOptional || URL)) WARNING.textContent = 'Add a label that tells readers where this link goes.';
            else if (VAGUE_LABEL.test(LABEL)) WARNING.textContent = 'Use a more specific label that describes the destination.';
            else if (LABEL && (LABEL === URL || /^(?:https?:\/\/|mailto:|tel:|\/|#)/i.test(LABEL))) WARNING.textContent = 'Describe the destination instead of using its URL as the label.';
            WARNING.hidden = !WARNING.textContent;
        }
        input.addEventListener('input', updateWarning);
        urlInput?.addEventListener('input', updateWarning);
        updateWarning();
    }

    function register(type, title, fields, defaults = {}, extra = null) {
        TOOLS[type] = class {
            static get toolbox() {
                return { title, icon: '<svg width="18" height="18" viewBox="0 0 18 18"><path d="M3 3h12v12H3zM6 7h6M6 10h6" fill="none" stroke="currentColor"/></svg>' };
            }
            static get isReadOnlySupported() { return true; }
            constructor({ data, readOnly }) {
                this.data = Object.assign({}, defaults, data);
                this.isReadOnly = readOnly;
            }
            render() {
                this.root = element('div', 'spages-tool');
                this.root.append(element('div', 'small fw-semibold text-body-secondary mb-2', title));
                fields.forEach(DEFINITION => field(this.root, DEFINITION[0], DEFINITION[1], this.data[DEFINITION[0]], DEFINITION[2] || {}));
                fields.filter(DEFINITION => DEFINITION[2]?.linkLabel).forEach(DEFINITION => {
                    const INPUT = this.root.querySelector('[data-field="' + DEFINITION[0] + '"]');
                    const URL_FIELD = this.root.querySelector('[data-field="' + (DEFINITION[2].linkUrlField || 'url') + '"]');
                    linkLabelWarning(INPUT, URL_FIELD, !!DEFINITION[2].optional);
                });
                if (extra) extra(this.root, this.data, this.isReadOnly);
                if (this.isReadOnly) {
                    this.root.querySelectorAll('input,textarea,select,button').forEach(CONTROL => { CONTROL.disabled = true; });
                }
                return this.root;
            }
            save(root) {
                const DATA = Object.assign({}, this.data);
                root.querySelectorAll('[data-field]').forEach(INPUT => {
                    DATA[INPUT.dataset.field] = INPUT.type === 'checkbox' ? INPUT.checked : INPUT.type === 'number' ? Number(INPUT.value) : INPUT.value;
                });
                if (type === 'list') DATA.items = DATA.lines.split('\n').filter(LINE => LINE.trim());
                if (type === 'cards') DATA.items = Array.from(root.querySelectorAll('[data-card]')).map(CARD => Object.fromEntries(Array.from(CARD.querySelectorAll('[data-card-field]')).map(INPUT => [INPUT.dataset.cardField, INPUT.value])));
                if (type === 'table') DATA.content = Array.from(root.querySelectorAll('tr')).map(ROW => Array.from(ROW.querySelectorAll('input')).map(INPUT => INPUT.value));
                return DATA;
            }
        };
    }

    register('header', 'Heading', [
        ['text', 'Heading text'],
        ['level', 'Heading level', { choices: [[2, 'H2 — section'], [3, 'H3 — subsection'], [4, 'H4'], [5, 'H5'], [6, 'H6']] }],
        ['anchor', 'Link anchor (optional)', { maxLength: 64, pattern: '[A-Za-z][A-Za-z0-9_-]{0,63}', placeholder: 'section-name', help: 'Starts with a letter; use letters, numbers, hyphens, or underscores.' }]
    ], { level: 2 });
    register('quote', 'Quote', [['text', 'Quotation', { multiline: true }], ['caption', 'Attribution']]);
    register('code', 'Code', [['code', 'Code (displayed as text)', { multiline: true }]]);
    register('delimiter', 'Divider', []);
    register('legacyMarkdown', 'Markdown', [['text', 'Markdown content', { multiline: true }]]);
    register('callout', 'Callout', [['title', 'Title'], ['text', 'Message', { multiline: true }]]);
    register('button', 'Button', [['text', 'Useful link label', { linkLabel: true }], ['url', 'Destination URL']]);
    register('snippet', 'Reusable snippet', [['key', 'Snippet', { choices: [['', 'Choose a published snippet'], ...SNIPPETS.map(SNIPPET => [SNIPPET.key, SNIPPET.title])] }]]);
    register('list', 'List', [['style', 'Style', { choices: [['unordered', 'Bullets'], ['ordered', 'Numbered']] }]], { items: [], style: 'unordered' }, (root, data) => {
        field(root, 'lines', 'One item per line', data.items.map(ITEM => typeof ITEM === 'string' ? ITEM : ITEM.content).join('\n'), { multiline: true });
    });
    register('cards', 'Cards', [], { items: [] }, (root, data) => {
        function addCard(item = {}) {
            const CARD = element('fieldset', 'border rounded p-3 mb-2');
            const INPUTS = {};
            CARD.dataset.card = '';
            CARD.append(element('legend', 'h6', 'Card'));
            ['title', 'text', 'url', 'label'].forEach(KEY => {
                const INPUT = field(CARD, KEY, { title: 'Title', text: 'Description', url: 'Link URL (optional)', label: 'Useful link label' }[KEY], item[KEY]);
                delete INPUT.dataset.field;
                INPUT.dataset.cardField = KEY;
                INPUTS[KEY] = INPUT;
            });
            linkLabelWarning(INPUTS.label, INPUTS.url, true);
            const REMOVE_BUTTON = element('button', 'btn btn-sm btn-outline-danger mt-2', 'Remove card');
            REMOVE_BUTTON.type = 'button';
            REMOVE_BUTTON.addEventListener('click', () => { CARD.remove(); changed(); });
            CARD.append(REMOVE_BUTTON);
            root.insertBefore(CARD, ADD_BUTTON);
        }
        const ADD_BUTTON = element('button', 'btn btn-sm btn-default', 'Add card');
        ADD_BUTTON.type = 'button';
        ADD_BUTTON.addEventListener('click', () => {
            if (root.querySelectorAll('[data-card]').length < 12) {
                addCard();
                changed();
            }
        });
        root.append(ADD_BUTTON);
        data.items.forEach(addCard);
    });
    register('table', 'Table', [['caption', 'Table caption'], ['withHeadings', 'First row contains column headings', { checkbox: true }]], { withHeadings: true, content: [['', ''], ['', '']] }, (root, data) => {
        const SCROLL = element('div', 'table-responsive');
        const TABLE = element('table', 'table table-sm');
        function addCell(row, value) {
            const CELL = element('td');
            const INPUT = element('input', 'form-control');
            INPUT.value = value;
            INPUT.setAttribute('aria-label', 'Row ' + (row.rowIndex + 1) + ', column ' + (row.cells.length + 1));
            CELL.append(INPUT);
            row.append(CELL);
        }
        function addRow(values) {
            const ROW = element('tr');
            TABLE.append(ROW);
            values.forEach(VALUE => addCell(ROW, VALUE));
        }
        SCROLL.append(TABLE);
        root.append(SCROLL);
        data.content.forEach(addRow);
        const ADD_ROW_BUTTON = element('button', 'btn btn-sm btn-default me-2', 'Add row');
        ADD_ROW_BUTTON.type = 'button';
        ADD_ROW_BUTTON.addEventListener('click', () => { addRow(Array(TABLE.rows[0]?.cells.length || 2).fill('')); changed(); });
        const ADD_COLUMN_BUTTON = element('button', 'btn btn-sm btn-default', 'Add column');
        ADD_COLUMN_BUTTON.type = 'button';
        ADD_COLUMN_BUTTON.addEventListener('click', () => { Array.from(TABLE.rows).forEach(ROW => addCell(ROW, '')); changed(); });
        root.append(ADD_ROW_BUTTON, ADD_COLUMN_BUTTON);
    });

    function mediaPicker(root, data, isImage, options = {}) {
        const CHOICES = [['', isImage ? 'No image selected' : 'No file selected']];
        if (data.att_id) CHOICES.push([data.att_id, 'Current attachment #' + data.att_id]);
        const SELECT = field(root, 'att_id', options.label || (isImage ? 'Image' : 'File'), data.att_id, { choices: CHOICES });
        const NOTE = element('p', 'small text-body-secondary', pageId ? 'Loading the file library…' : 'Choose a file now; the draft will be saved before upload.');
        const UPLOAD = field(root, '', options.uploadLabel || 'Upload a page-owned file', '');
        let isActivated = false;
        SELECT.disabled = !!options.isReadOnly;
        if (options.valueTarget) SELECT.addEventListener('change', () => { options.valueTarget.value = SELECT.value; });
        root.append(NOTE);
        delete UPLOAD.dataset.field;
        UPLOAD.type = 'file';
        UPLOAD.disabled = !!options.isReadOnly;
        if (isImage) UPLOAD.accept = 'image/*';
        async function activate() {
            if (isActivated || !pageId) return;
            isActivated = true;
            const SELECTED = String(SELECT.value || data.att_id || '');
            NOTE.textContent = 'Loading the file library…';
            try {
                const RESPONSE = await fetch(BASE_URL + '/(Media)/' + pageId, {
                    credentials: 'same-origin',
                    headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
                });
                const RESULT = await RESPONSE.json();
                if (!RESPONSE.ok || RESULT.error) throw new Error(RESULT.error?.message || 'The file library could not be loaded.');
                (RESULT.items || []).filter(ITEM => !isImage || ITEM.is_image === true || Number(ITEM.is_image) === 1).forEach(ITEM => {
                    const IS_PRESENT = Array.from(SELECT.options).some(OPTION => OPTION.value === String(ITEM.id));
                    if (!IS_PRESENT) {
                        const OPTION = element('option', '', ITEM.iname || ITEM.fname || 'Attachment #' + ITEM.id);
                        OPTION.value = ITEM.id;
                        SELECT.append(OPTION);
                    }
                });
                SELECT.value = SELECTED;
                NOTE.className = 'small text-body-secondary';
                NOTE.textContent = 'Choose an existing file or upload a new one.';
            } catch (error) {
                isActivated = false;
                NOTE.className = 'text-danger small';
                NOTE.textContent = error.message;
            }
        }
        UPLOAD.addEventListener('change', async () => {
            if (options.isReadOnly || !UPLOAD.files.length) return;
            const FILE = UPLOAD.files[0];
            UPLOAD.disabled = true;
            try {
                if (!pageId) {
                    if (!FORM.reportValidity()) throw new Error('Complete the required page details before uploading this file. Your selection is still here.');
                    await Promise.resolve();
                    await saveDraft(true);
                }
                if (!pageId) throw new Error('Save the draft before uploading this file. Your selection is still here.');
                await activate();
                const PAYLOAD = new FormData();
                PAYLOAD.set('XSS', FORM.elements.XSS.value);
                PAYLOAD.set('file', FILE);
                const RESULT = await request(BASE_URL + '/(Upload)/' + pageId, PAYLOAD);
                let option = Array.from(SELECT.options).find(OPTION => OPTION.value === String(RESULT.id));
                if (!option) {
                    option = element('option', '', RESULT.name);
                    option.value = RESULT.id;
                    SELECT.append(option);
                }
                SELECT.value = RESULT.id;
                if (options.valueTarget) options.valueTarget.value = RESULT.id;
                UPLOAD.value = '';
                markChanged();
            } catch (error) {
                showError(error);
            } finally {
                UPLOAD.disabled = !!options.isReadOnly;
            }
        });
        const PICKER = { activate };
        MEDIA_PICKERS.push(PICKER);
        void activate();
        return PICKER;
    }

    register('image', 'Image', [['alt', 'Alternative text'], ['decorative', 'Decorative — conveys no information', { checkbox: true }], ['caption', 'Caption (optional)']], { decorative: false }, (root, data, isReadOnly) => {
        mediaPicker(root, data, true, { isReadOnly });
    });
    register('file', 'File', [['title', 'Descriptive file link label', { linkLabel: true }]], {}, (root, data, isReadOnly) => {
        mediaPicker(root, data, false, { isReadOnly });
    });
    window.SpagesEditor = Object.assign(window.SpagesEditor || {}, { builtInTools: TOOLS });

    class UnavailableTool {
        static get isReadOnlySupported() { return true; }
        constructor({ data, readOnly }) {
            this.data = data;
            this.isReadOnly = readOnly;
        }
        render() {
            const ROOT = element('div', 'spages-tool spages-tool-unavailable');
            const IS_NESTED_SNIPPET = IS_SNIPPET && this.data.originalType === 'snippet';
            ROOT.append(element('div', 'fw-semibold', IS_NESTED_SNIPPET ? 'Nested reusable snippet' : 'Unavailable block: ' + (this.data.originalType || 'unknown')));
            ROOT.append(element('p', 'small mb-2', IS_NESTED_SNIPPET ? 'Reusable snippets cannot contain another snippet. Remove or replace this block; its original JSON remains available below.' : 'This site does not have the editor control for this block. Its original JSON is kept below and will be saved intact.'));
            this.input = field(ROOT, 'raw', 'Original block JSON', this.data.raw, {
                multiline: true,
                help: 'Correct the JSON here or install the matching editor tool. Invalid JSON blocks saving so the original content cannot be lost.'
            });
            this.input.rows = 8;
            if (this.isReadOnly) this.input.disabled = true;
            return ROOT;
        }
        save() {
            return { originalType: this.data.originalType, raw: this.input.value };
        }
    }

    const CONFIGURED_TOOLS = Object.assign({}, TOOLS, window.SpagesEditor.tools || {});
    if (IS_SNIPPET) delete CONFIGURED_TOOLS.snippet;
    const AVAILABLE_TOOLS = new Set(['paragraph', ...Object.entries(CONFIGURED_TOOLS).filter(([, VALUE]) => typeof VALUE === 'function' || typeof VALUE?.class === 'function').map(([NAME]) => NAME)]);
    const UNAVAILABLE_TYPE = unavailableToolName(CONFIGURED_TOOLS);
    CONFIGURED_TOOLS[UNAVAILABLE_TYPE] = { class: UnavailableTool, toolbox: false };

    function unavailableToolName(configuredTools) {
        let name = 'spagesUnavailable';
        while (Object.prototype.hasOwnProperty.call(configuredTools, name)) name = '_' + name;
        return name;
    }

    function prepareRegion(region) {
        const PREPARED = Object.assign({}, region || { blocks: [] });
        PREPARED.blocks = Array.isArray(PREPARED.blocks) ? PREPARED.blocks.map(BLOCK => {
            if (BLOCK && AVAILABLE_TOOLS.has(BLOCK.type)) return BLOCK;
            const ORIGINAL_TYPE = typeof BLOCK?.type === 'string' ? BLOCK.type : 'unknown';
            return { type: UNAVAILABLE_TYPE, data: { originalType: ORIGINAL_TYPE, raw: JSON.stringify(BLOCK, null, 2) } };
        }) : [];
        return PREPARED;
    }

    function restoreUnavailable(region, regionName) {
        region.blocks = region.blocks.map(BLOCK => {
            if (BLOCK.type !== UNAVAILABLE_TYPE) return BLOCK;
            let original;
            try {
                original = JSON.parse(BLOCK.data.raw);
            } catch (_) {
                throw new Error('The unavailable “' + BLOCK.data.originalType + '” block in ' + regionName + ' has invalid JSON. Correct its raw JSON before saving; your input is still here.');
            }
            const IS_VALID = original && !Array.isArray(original) && typeof original === 'object' && typeof original.type === 'string' && original.data && !Array.isArray(original.data) && typeof original.data === 'object';
            if (!IS_VALID) throw new Error('The unavailable “' + BLOCK.data.originalType + '” block in ' + regionName + ' must remain an object with a type and data object. Correct its raw JSON before saving; your input is still here.');
            return original;
        });
        return region;
    }

    const SLOT = document.getElementById('spages-slot');
    SNIPPETS.forEach(SNIPPET => {
        const OPTION = element('option', '', SNIPPET.title);
        OPTION.value = SNIPPET.key;
        SLOT.append(OPTION);
    });
    SLOT.value = documentData.slots?.after_content || '';
    if (IS_SNIPPET) {
        SLOT.closest('label').hidden = true;
        URL_INPUT.required = true;
        URL_INPUT.maxLength = 64;
        URL_INPUT.pattern = '[a-z][a-z0-9_-]{0,63}';
        URL_INPUT.readOnly = pageId > 0;
    }

    const HEAD_MEDIA = document.getElementById('spages-head-media');
    if (HEAD_MEDIA) {
        mediaPicker(HEAD_MEDIA, { att_id: Number(HEAD_MEDIA.dataset.attId || 0) }, true, {
            label: 'Page image',
            uploadLabel: 'Upload a page image',
            valueTarget: document.getElementById('spages-head-att'),
            isReadOnly: !IS_AUTHOR
        });
    }

    function updateLayout() {
        const LAYOUT = LAYOUTS[document.getElementById('spages-layout').value];
        document.querySelectorAll('[data-region]').forEach(REGION => {
            REGION.hidden = !LAYOUT.regions.includes(REGION.dataset.region);
        });
        SLOT.closest('label').hidden = !LAYOUT.slots.includes('after_content') || IS_SNIPPET;
    }

    async function collect() {
        const REGION_LABELS = { main: 'page content', left: 'left column', right: 'right column' };
        for (const [REGION_NAME, EDITOR] of Object.entries(EDITORS)) {
            documentData.regions[REGION_NAME] = restoreUnavailable(await EDITOR.save(), REGION_LABELS[REGION_NAME]);
        }
        documentData.slots = { ...documentData.slots, after_content: SLOT.value };
        DOCUMENT_FIELD.value = JSON.stringify(documentData);
    }

    async function saveDraft(isAutosave = false) {
        if (savePromise) {
            await savePromise;
            if (isAutosave && !isDirty) return;
        }
        if (isAutosave && (!isDirty || !document.getElementById('spages-title').value.trim() || !FORM.checkValidity())) return;
        if (!isAutosave && !FORM.reportValidity()) throw new Error('Complete the required fields before saving.');
        const STARTED_SEQUENCE = changeSequence;
        savePromise = (async () => {
            STATE.textContent = 'Saving draft…';
            await collect();
            const BODY = new FormData(FORM);
            BODY.set('autosave', isAutosave ? '1' : '0');
            const RESULT = await request(BASE_URL + '/' + pageId, BODY);
            pageId = RESULT.id;
            if (!Number(FORM.dataset.id)) {
                FORM.dataset.id = String(pageId);
                history.replaceState(null, '', RESULT.location);
            }
            if (IS_SNIPPET) URL_INPUT.readOnly = true;
            await Promise.all(MEDIA_PICKERS.map(PICKER => PICKER.activate()));
            isDirty = changeSequence !== STARTED_SEQUENCE;
            STATE.textContent = isDirty ? 'Unsaved changes' : 'Draft saved at ' + new Date().toLocaleTimeString();
            ERROR_BOX.hidden = true;
        })();
        try {
            await savePromise;
        } finally {
            savePromise = null;
        }
    }

    function updateWorkflowActions(workflow, isScheduled) {
        const SUBMIT_BUTTON = FORM.querySelector('[data-workflow="Submit"]');
        const CHANGES_BUTTON = FORM.querySelector('[data-workflow="Changes"]');
        const CANCEL_BUTTON = FORM.querySelector('[data-workflow="Cancel"]');
        if (SUBMIT_BUTTON) SUBMIT_BUTTON.hidden = workflow === WORKFLOW_IN_REVIEW;
        if (CHANGES_BUTTON) CHANGES_BUTTON.hidden = workflow !== WORKFLOW_IN_REVIEW;
        if (CANCEL_BUTTON) CANCEL_BUTTON.hidden = !isScheduled;
    }

    FORM.addEventListener('submit', EVENT => {
        EVENT.preventDefault();
        clearTimeout(autosaveTimer);
        saveDraft(false).catch(showError);
    });
    FORM.addEventListener('input', changed);
    FORM.addEventListener('change', changed);
    document.getElementById('spages-layout').addEventListener('change', updateLayout);
    updateWorkflowActions(Number(FORM.dataset.workflow), isTrue(FORM.dataset.isScheduled));

    FORM.querySelector('[data-preview]').addEventListener('click', async () => {
        const PREVIEW = window.open('about:blank', '_blank');
        try {
            if (!pageId || isDirty) await saveDraft(true);
            if (pageId && PREVIEW) {
                PREVIEW.opener = null;
                PREVIEW.location = BASE_URL + '/(Preview)/' + pageId;
            } else {
                PREVIEW?.close();
            }
        } catch (error) {
            PREVIEW?.close();
            showError(error);
        }
    });

    FORM.querySelectorAll('[data-workflow]').forEach(BUTTON => {
        BUTTON.addEventListener('click', async () => {
            BUTTON.disabled = true;
            clearTimeout(autosaveTimer);
            try {
                if (isDirty || !pageId) await saveDraft(false);
                const BODY = new FormData();
                BODY.set('XSS', FORM.elements.XSS.value);
                BODY.set('note', document.getElementById('spages-note').value);
                const DATE = document.getElementById('spages-publish-at')?.value;
                if (DATE) BODY.set('publish_at', new Date(DATE).toISOString());
                await request(BASE_URL + '/(' + BUTTON.dataset.workflow + ')/' + pageId, BODY);
                isDirty = false;
                STATE.textContent = 'Publication updated';
                ERROR_BOX.hidden = true;
                location.reload();
            } catch (error) {
                showError(error);
            } finally {
                BUTTON.disabled = false;
            }
        });
    });

    FORM.querySelectorAll('[data-restore]').forEach(BUTTON => {
        BUTTON.addEventListener('click', async () => {
            try {
                if (isDirty) await saveDraft(false);
                const BODY = new FormData();
                BODY.set('XSS', FORM.elements.XSS.value);
                BODY.set('revision_id', BUTTON.dataset.restore);
                await request(BASE_URL + '/(RestoreRevision)/' + pageId, BODY);
                isDirty = false;
                location.reload();
            } catch (error) {
                showError(error);
            }
        });
    });

    window.addEventListener('beforeunload', EVENT => {
        if (isDirty) {
            EVENT.preventDefault();
            EVENT.returnValue = '';
        }
    });

    if (!window.EditorJS) {
        showError(new Error('The editor assets could not be loaded. Restore the pinned assets with LibMan and reload.'));
        return;
    }
    for (const REGION_NAME of ['main', 'left', 'right']) {
        EDITORS[REGION_NAME] = new window.EditorJS({
            holder: 'spages-' + REGION_NAME,
            data: prepareRegion(documentData.regions[REGION_NAME]),
            minHeight: REGION_NAME === 'main' ? 220 : 100,
            placeholder: 'Start writing, or choose + to add a block',
            tools: CONFIGURED_TOOLS,
            readOnly: !IS_AUTHOR,
            onChange: changed
        });
    }
    Promise.all(Object.values(EDITORS).map(EDITOR => EDITOR.isReady)).then(() => {
        isReady = true;
        updateLayout();
    }).catch(showError);
}());
