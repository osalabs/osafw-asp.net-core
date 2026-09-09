// Shared interaction behavior; authorization and validation remain server responsibilities.
function normalizedColumnWidths(value, headers) {
    if (typeof value === 'string') {
        try { value = JSON.parse(value); } catch { value = {}; }
    }
    const widths = {};
    for (const header of (headers ?? [])) {
        if (Object.keys(widths).length >= 100) break;
        const field = header.field_name;
        const raw = value?.[field];
        if (!["number", "string"].includes(typeof raw)) continue;
        const width = Number(raw);
        if (Number.isFinite(width) && width > 0) widths[field] = Math.max(60, Math.min(800, Math.round(width)));
    }
    return widths;
}

const columnWidthWrites = new WeakMap();
function createInteractionActions() {
    return {
        canAction(action, row = null) {
            if (this.is_readonly) return false;
            if (this.capabilities?.[action] === false) return false;
            if (row?._capabilities?.[action] === false || row?._meta?.is_ro) return false;
            return true;
        },
        canDeleteSelection() {
            return this.canAction('delete') && this.list_rows.filter(row => this.hchecked_rows[row[this.field_id]]).every(row => this.canAction('delete', row));
        },
        canSaveForm() {
            return this.canAction(this.edit_data?.id ? 'edit' : 'create', this.edit_data?.i)
                && this.edit_data?._capabilities?.[this.edit_data?.id ? 'edit' : 'create'] !== false;
        },
        filterVisibilityKey() {
            return ['fw.filters', location.origin, this.global?.ROOT_URL ?? '', this.base_url,
                this.is_list_edit ? 'edit' : 'list', this.related_id ?? 0].map(String).join('|');
        },
        restoreFilterVisibility() {
            this.is_filter_panel_open = true;
            try { this.is_filter_panel_open = localStorage.getItem(this.filterVisibilityKey()) !== 'closed'; } catch { }
        },
        toggleFilterPanel() {
            this.is_filter_panel_open = !this.is_filter_panel_open;
            try { localStorage.setItem(this.filterVisibilityKey(), this.is_filter_panel_open ? 'open' : 'closed'); } catch { }
        },
        columnWidths() {
            return normalizedColumnWidths(this.list_user_view?.widths, this.all_list_columns?.length ? this.all_list_columns : this.list_headers);
        },
        async saveColumnWidth(field, width) {
            const userView = this.list_user_view;
            const previous = userView.widths;
            const api = this.api;
            const isListEdit = this.is_list_edit;
            const widths = normalizedColumnWidths({ ...this.columnWidths(), [field]: width }, this.all_list_columns?.length ? this.all_list_columns : this.list_headers);
            this.list_user_view.widths = widths;
            const preceding = columnWidthWrites.get(this) ?? Promise.resolve();
            const write = preceding.catch(() => {}).then(async () => {
                try {
                    const response = await api.post('/(SaveUserViews)', {
                        XSS: this.XSS, is_list_edit: isListEdit, widths: JSON.stringify(widths)
                    });
                    if (response?.error || response?.success === false) throw { body: response };
                    return true;
                } catch (error) {
                    if (columnWidthWrites.get(this) === write && this.list_user_view === userView) userView.widths = previous;
                    this.handleError(error, 'saveColumnWidth');
                    return false;
                }
            });
            columnWidthWrites.set(this, write);
            return write;
        },
        formIssues(form = this.edit_data) {
            const response = form?.save_result ?? {};
            const issues = Array.isArray(response.validation_issues) ? response.validation_issues : [];
            const result = issues.filter(issue => issue && typeof issue.message === 'string')
                .map(issue => ({ ...issue, severity: issue.severity === 'warning' ? 'warning' : 'error' }));
            const details = response.error?.details;
            if (details && typeof details === 'object') {
                Object.entries(details).forEach(([field, code]) => {
                    if (field === 'REQUIRED' || field === 'INVALID') return;
                    if (result.some(issue => issue.field === field && issue.severity === 'error')) return;
                    result.push({ field, severity: 'error', message: code === true ? 'Required field' : (window.fwConst.ERR_CODES_MAP[code] ?? 'Invalid value') });
                });
            }
            return result;
        },
        fieldIssues(def, form) {
            const field = def.issue_field ?? def.field;
            const rowId = form?.row_id ?? form?.i?.id;
            return this.formIssues(this.edit_data).filter(issue => issue.field === field
                && (issue.row_id === undefined || String(issue.row_id) === String(rowId)));
        },
        async focusFormIssue(issue, root) {
            if (!issue.field || !root) return;
            let tab = issue.tab;
            if (tab === undefined) {
                tab = Object.keys(this.showform_fields_tabs ?? {}).find(key =>
                    this.showform_fields_tabs[key].some(def => (def.issue_field ?? def.field) === issue.field));
            }
            if (tab !== undefined) this.setFormTab(tab);
            await new Promise(resolve => requestAnimationFrame(resolve));
            const matches = Array.from(root.querySelectorAll('[data-fw-field]'));
            const target = matches.find(element => element.dataset.fwField === issue.field &&
                (issue.row_id === undefined || element.dataset.fwRow === String(issue.row_id)));
            if (!target) return;
            target.dispatchEvent(new CustomEvent('fw-reveal-field', { bubbles: true }));
            await new Promise(resolve => requestAnimationFrame(resolve));
            const control = target.querySelector('input:not([type=hidden]):not(:disabled),select:not(:disabled),textarea:not(:disabled),button:not(:disabled),[tabindex]');
            (control ?? target).focus();
            target.scrollIntoView({ block: 'nearest' });
        },
        async refreshQuickEditList() {
            const pane = document.querySelector('.list-edit-pane');
            const scrollTop = pane?.scrollTop;
            const active = document.activeElement;
            const selection = active && typeof active.selectionStart === 'number' ? [active.selectionStart, active.selectionEnd] : null;
            const data = await this.api.get('', { query: { ...this.listRequestQuery, scope: 'list_rows' } });
            this.saveToStore(data);
            this.applyDefaultsAfterLoad(data);
            this.onLoadIndexSuccess();
            await new Promise(resolve => requestAnimationFrame(resolve));
            if (pane && this.is_list_edit_pane) pane.scrollTop = scrollTop;
            if (active?.isConnected && pane?.contains(active)) {
                active.focus({ preventScroll: true });
                if (selection) active.setSelectionRange(...selection);
            }
        }
    };
}
