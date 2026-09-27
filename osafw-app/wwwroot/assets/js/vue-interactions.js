// Shared interaction behavior; authorization and validation remain server responsibilities.
// Retained for customized templates that import the original width defaults.
export const LIST_TABLE_COLUMN_WIDTHS = Object.freeze({
    selection: 40,
    standard: 160
});

function normalizedColumnWidths(value, headers) {
    if (typeof value === 'string') {
        try {
            value = JSON.parse(value);
        } catch {
            value = {};
        }
    }

    const widths = {};
    for (const header of (headers ?? [])) {
        if (Object.keys(widths).length >= 100) {
            break;
        }

        const field = header.field_name;
        const raw = value?.[field];
        if (!["number", "string"].includes(typeof raw)) {
            continue;
        }

        const width = Number(raw);
        if (Number.isFinite(width) && width > 0) {
            widths[field] = Math.max(60, Math.min(800, Math.round(width)));
        }
    }

    return widths;
}

const userViewWrites = new WeakMap();

function userViewWriteState(store) {
    let state = userViewWrites.get(store);
    if (!state) {
        state = {
            tail: Promise.resolve(),
            nextWidthId: 0,
            contexts: new WeakMap(),
            latestByView: new WeakMap()
        };
        userViewWrites.set(store, state);
    }

    return state;
}

// Widths, reset, named views, and density all update the same saved user view.
export function queueUserViewWrite(store, write) {
    const state = userViewWriteState(store);
    let modes = state.contexts.get(store.api);
    if (!modes) {
        modes = new Map();
        state.contexts.set(store.api, modes);
    }
    let context = modes.get(store.is_list_edit);
    if (!context) {
        context = { pending: 0, widths: {}, view: null };
        modes.set(store.is_list_edit, context);
    }
    if (!context.pending && (context.widths !== undefined || context.view !== store.list_user_view)) {
        context.widths = store.columnWidths();
        context.view = store.list_user_view;
    }
    context.pending++;
    const pending = state.tail.then(() => write(context)).finally(() => context.pending--);
    state.tail = pending.catch(() => {});
    return pending;
}

export const interactionActions = {
    canAction(action, row = null) {
        if (this.is_readonly) {
            return false;
        }
        if (this.capabilities?.[action] === false) {
            return false;
        }
        if (row?._capabilities?.[action] === false || row?._meta?.is_ro) {
            return false;
        }

        return true;
    },
    canDeleteSelection() {
        return this.canAction('delete')
            && this.list_rows
                .filter(row => this.hchecked_rows[row[this.field_id]])
                .every(row => this.canAction('delete', row));
    },
    canSaveForm() {
        return this.canAction(this.edit_data?.id ? 'edit' : 'create', this.edit_data?.i)
            && this.edit_data?._capabilities?.[this.edit_data?.id ? 'edit' : 'create'] !== false;
    },
    filterVisibilityKey() {
        return [
            'fw.filters',
            location.origin,
            this.global?.ROOT_URL ?? '',
            this.base_url,
            this.is_list_edit ? 'edit' : 'list',
            this.related_id ?? 0
        ].map(String).join('|');
    },
    restoreFilterVisibility() {
        this.is_filter_panel_open = true;
        try {
            this.is_filter_panel_open = localStorage.getItem(this.filterVisibilityKey()) !== 'closed';
        } catch {
        }
    },
    toggleFilterPanel() {
        this.is_filter_panel_open = !this.is_filter_panel_open;
        try {
            localStorage.setItem(this.filterVisibilityKey(), this.is_filter_panel_open ? 'open' : 'closed');
        } catch {
        }
    },
    columnWidths() {
        const headers = this.all_list_columns?.length ? this.all_list_columns : this.list_headers;
        return normalizedColumnWidths(this.list_user_view?.widths, headers);
    },
    async saveColumnWidth(field, width) {
        return this.saveColumnWidths({ [field]: width });
    },
    async saveColumnWidths(changes) {
        let userView = this.list_user_view;
        const api = this.api;
        const xss = this.XSS;
        const isListEdit = this.is_list_edit;
        const headers = this.all_list_columns?.length ? this.all_list_columns : this.list_headers;
        const state = userViewWriteState(this);
        const entry = { id: ++state.nextWidthId };

        const write = queueUserViewWrite(this, async context => {
            // A preceding reset/load refreshes the view. Apply this resize to that new base.
            if (this.api === api && this.is_list_edit === isListEdit && this.list_user_view !== userView) {
                if (state.latestByView.get(userView) === entry) {
                    state.latestByView.delete(userView);
                }
                userView = this.list_user_view;
                if ((state.latestByView.get(userView)?.id ?? 0) < entry.id) {
                    state.latestByView.set(userView, entry);
                }
            }

            try {
                const confirmed = context.widths;
                if (confirmed === undefined) {
                    throw { body: { error: { message: window.fwConst.ERR_CODES_MAP.SAVE_FAILED } } };
                }
                const widths = normalizedColumnWidths({ ...confirmed, ...changes }, headers);
                if (state.latestByView.get(userView) === entry) {
                    userView.widths = widths;
                }
                const response = await api.post('/(SaveUserViews)', {
                    XSS: xss,
                    is_list_edit: isListEdit,
                    widths: JSON.stringify(widths)
                });
                if (response?.error || response?.success === false) {
                    throw { body: response };
                }

                context.widths = widths;
                return true;
            } catch (error) {
                this.handleError(error, 'saveColumnWidths');
                return false;
            } finally {
                if (state.latestByView.get(userView) === entry) {
                    userView.widths = context.widths ?? {};
                    state.latestByView.delete(userView);
                }
            }
        });

        userView.widths = normalizedColumnWidths({ ...this.columnWidths(), ...changes }, headers);
        state.latestByView.set(userView, entry);
        return write;
    },
    formIssues(form = this.edit_data) {
        const response = form?.save_result ?? {};
        const status = Number(response.error?.code);
        if (!response.failed_tabs && status > 400 && status !== 422) {
            return [];
        }
        const issues = Array.isArray(response.form_issues) ? response.form_issues : [];
        const result = issues
            .filter(issue => issue && typeof issue.message === 'string')
            .map(issue => ({ ...issue, severity: issue.severity === 'warning' ? 'warning' : 'error' }));
        if (response.failed_tabs) {
            return result; // Already collected per tab, including separation of non-validation failures.
        }
        const details = response.error?.details;
        if (details && typeof details === 'object') {
            Object.entries(details).forEach(([field, code]) => {
                if (field === 'REQUIRED' || field === 'INVALID') {
                    return;
                }
                if (result.some(issue => issue.field === field && issue.severity === 'error')) {
                    return;
                }

                result.push({
                    field,
                    severity: 'error',
                    message: code === true
                        ? window.fwConst.ERR_CODES_MAP.REQUIRED
                        : (window.fwConst.ERR_CODES_MAP[code] ?? window.fwConst.ERR_CODES_MAP.INVALID)
                });
            });
        }

        return result;
    },
    fieldIssues(def, form) {
        const field = def.issue_field ?? def.field;
        const rowId = form?.row_id ?? form?.i?.id;
        return this.formIssues(form?.save_result ? form : this.edit_data).filter(issue => issue.field === field
            && (issue.tab === undefined || issue.tab === this.activeFormTab)
            && (issue.row_id === undefined || String(issue.row_id) === String(rowId)));
    },
    issueLabel(issue) {
        const fields = [...(this.showform_fields_tabs?.[issue.tab] ?? []), ...(this.showform_fields ?? []), ...Object.values(this.showform_fields_tabs ?? {}).flat()];
        const child = /^item-(.+)#([^[]+)\[([^\]]+)\]$/.exec(issue.field ?? '');
        const field = child ? child[1] : issue.field;
        const def = fields.find(def => (def.issue_field ?? def.field) === field);
        if (child) {
            const column = def?.showform_fields?.find(def => def.field === child[3]);
            return (def?.label ?? field) + ' ' + (column?.label ?? child[3]);
        }
        return def?.label ?? field;
    },
    async focusFormIssue(issue, root) {
        if (!issue.field || !root) {
            return;
        }

        let tab = issue.tab;
        if (tab === undefined) {
            tab = Object.keys(this.showform_fields_tabs ?? {}).find(key =>
                this.showform_fields_tabs[key].some(def => (def.issue_field ?? def.field) === issue.field));
        }
        if (tab !== undefined) {
            this.setFormTab(tab);
        }

        await new Promise(resolve => requestAnimationFrame(resolve));
        const matches = Array.from(root.querySelectorAll('[data-fw-field]'));
        const target = matches.find(element => element.dataset.fwField === issue.field
            && (issue.row_id === undefined || element.dataset.fwRow === String(issue.row_id)));
        if (!target) {
            return;
        }

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
        const selection = active && typeof active.selectionStart === 'number'
            ? [active.selectionStart, active.selectionEnd]
            : null;
        const data = await this.api.get('', { query: { ...this.listRequestQuery, scope: 'list_rows' } });
        this.saveToStore(data);
        this.applyDefaultsAfterLoad(data);
        this.onLoadIndexSuccess();
        await new Promise(resolve => requestAnimationFrame(resolve));
        if (pane && this.is_list_edit_pane) {
            pane.scrollTop = scrollTop;
        }
        if (active?.isConnected && pane?.contains(active)) {
            active.focus({ preventScroll: true });
            if (selection) {
                active.setSelectionRange(...selection);
            }
        }
    }
};
