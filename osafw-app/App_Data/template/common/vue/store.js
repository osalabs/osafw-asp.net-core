// define some global constants
window.fwConst = {
    ERR_CODES_MAP: {
        REQUIRED: 'Required field',
        EXISTS: 'This name already exists in our database',
        WRONG: 'Invalid',
        EMAIL: 'Invalid Email',
    },
};
<~/common/vue/store_core.js>
<~/common/vue/interaction_helpers.js>

let state = {
    global: {}, //global config
    flash: {}, //flash messages from server to display on startup
    XSS: '', // token
    me_id: 0, // current user id
    access_level: 0, // user access level
    base_url: '', // base url for the controller
    api: null, // mande instance
    list_title: '', //list screen title
    view_title: '',
    edit_title: '',
    add_new_title: '',
    is_readonly: false,
    capabilities: {},
    is_filter_panel_open: true,
    quick_edit_keep_context: false,
    edit_save_states: [], // in-flight requests and coalesced followups, scoped to the captured form
    is_activity_logs: false, //true if activity logs enabled

    // default UI options, override in specific controller via store.js
    uioptions: {
        //top keys - screens
        list: {
            header: { // list-header, can be false
                btnAddNew: true,
                count: true,
                buttons: [] // custom - label, url, icon, title (for tooltip), class, post(true|false)
            },
            filters: { // list-filters, can be false
                s: { // search input, can be false
                    placeholder: ""
                },
                status: true, // status dropdown
                userlists: true, // userlists dropdown
                export: true, // export btn
                print: true, // print btn
                tableButtons: true, // table buttons
            },
            table: { //list-table
                isButtonsLeft: null, //null - use global.is_list_btn_left
                rowTitle: 'Double click to Edit',
                nl2br: false, // true if table cells should show line breaks
                maxRowHeight: 0, // max row height in px, 0 - no limit
                rowButtons: { // list-row-btn, can be false as whole
                    view: true,
                    edit: true,
                    quickedit: true,
                    delete: true,
                    buttons: [] // custom - label, url (/id will be appended), icon, title (for tooltip), class, post(true|false)
                },
                rowDblClick: 'view', // view|edit|quickedit or else override store onRowDblClick
                pagination: { // list-pagination, can be false
                    count: false,
                }
            },
            btnMulti: { //list-btn-multi, can be false
                isDelete: true,
                isUserlists: true,
                buttons: [], // custom - label, url, icon, title (for tooltip), class
            }
        },
        view: {
            header: { // view-header, can be false
                btnAddNew: true,
            },
        },
        edit: {
            header: { // edit-header, can be false
                btnAddNew: true,
            },
        }
    },

    // user views
    all_list_columns: [], // list of all available columns
    list_user_view: {}, // UserViews record for current controller
    select_userviews: [], // list of available user views
    userviews_url: '/My/Views',

    // my lists
    is_userlists: false,
    select_userlists: [],
    my_userlists: [],
    userlists_url: '/My/Lists',
    userlists_new_name: '', // v-model name for creating new list

    // list edit support
    is_list_edit: false, //true if list rows inline-editable

    // list filter, can contain: {pagenum:N, pagesize:N, sortby:'', sortdir:'asc|desc'}
    f: {
        pagenum: 0,
        pagesize: 25,
        sortby: '',
        sortdir: '',
        s: '',
        status: '',
        userlist: '',
    },

    related_id: 0, // related model id
    return_url: '', // return url if controller called from other place expecting user's return
    return_title: '', // label for return_url breadcrumb
    field_id: 'id', // model's id field name
    view_list_custom: [], // used for cellFormatter
    view_list_custom_trusted: [], // custom list fields allowed to render cellFormatter HTML
    list_calculated_fields: [], // calculated list fields; source dependencies remain server-side
    list_headers: [], // list headers, array of {field_name:"", field_name_visible:"", is_sortable:bool, is_checked:bool, search_value:null|"", is_ro:bool, input_type:"input|select|date"}
    is_list_search_open: false, // true if list search is open by user
    count: 0, // total list rows count
    list_rows: [], // array of row objects to display, row can contain _meta object {is_ro:bool, ro_fields:[read only field names]}
    pager: [], // array of { pagenum:N, pagenum_show:N, is_cur_page:0|1, is_show_first:0|1, is_show_prev:0|1, is_show_next:0|1, pagenum_next:N}

    // edit form fields configuration
    list_editable_def_types: ['input', 'email', 'number', 'range', 'textarea', 'date_combo', 'date_popup', 'datetime_popup', 'datetime_local', 'time', 'autocomplete', 'select', 'cb', 'switch', 'radio', 'yesno'],
    form_tabs: [],
    show_fields: [],
    show_fields_tabs: {},
    showform_fields: [],
    showform_fields_tabs: {},
    current_form_tab: '',
    is_list_edit_pane: false, // true if edit pane is open
    edit_data: null, // object for single item edit form {id:X, i:{}, multi_rows:{}, subtables:{}, attachments:{}, att_links:[att_ids], add_users_id_name:'', upd_users_id_name:'', save_result:{}}}

    //standard lookups
    lookups_std: {
        statusf: [
            { id: 0, iname: 'Active', bgcolor: 'text-bg-success' },
            { id: 10, iname: 'Inactive', bgcolor: 'text-bg-secondary' }
        ],
        statusf_admin: [
            { id: 0, iname: 'Active' },
            { id: 10, iname: 'Inactive' },
            { id: 127, iname: '[Deleted]' }
        ]
    },
    //entity-related lookups
    lookups: {},

    //work vars
    current_screen: '', // current screen name - list/view/edit
    current_id: 0, // current item id for view/edit screens

    hchecked_rows: {}, // array of checked rows {row[id_field] => 1}
    loadIndexDebouncedTimeout: null,
    saveEditDataDebouncedTimeout: null,
    is_initial_load: true, //reset after initial load
    is_loading_index: false, //true while loading index data
    is_loading_item: false, //true while loading item data
    loading_progress: 0,
    loading_progress_timer: null,
    loading_progress_timeout: null,
    cells_saving: {}, // cells saving status {row[id_field] => true}
    cells_errors: {}, // cells saving status {row[id_field] => true}
};

// merge in fwStoreState if defined
state = mergeStoreDefaults(state, fwStoreState);

function hasListSearchValue(value) {
    if (value === null || value === undefined) return false;
    if (typeof value === 'string') return value.length > 0;
    if (Array.isArray(value)) return value.length > 0;
    if (typeof value === 'object') return Object.keys(value).length > 0;
    return true;
}

function serializeListSearchValue(value) {
    if (value === null || value === undefined) return '';
    if (typeof value === 'string') return value;
    return JSON.stringify(value);
}

function editFormSaveSnapshot(form) {
    // Match the JSON request boundary; later v-model changes must not mutate a submitted request.
    return JSON.parse(JSON.stringify({ id: form.id, i: form.i, subtables: form.subtables,
        multi_rows: form.multi_rows, att_links: form.att_links, att_files: form.att_files }));
}

const editFormSaveDebounces = new WeakMap();
const editFormSaveFailures = new WeakMap();
const editFormSaveContextKey = Symbol();
function editFormSaveContext(store) {
    return {
        [editFormSaveContextKey]: true,
        tab: store.activeFormTab,
        tabLabel: store.form_tabs?.find(tab => tab.tab === store.activeFormTab)?.label ?? store.activeFormTab,
        // The standard server processes compound fields only from this tab, even when
        // its response contains other submitted subtables. Untabbed extensions keep their existing merge.
        subtableFields: store.form_tabs?.length
            ? store.tabbedShowFormFields.filter(def => def.type === 'subtable_edit').map(def => def.field) : null,
    };
}

function processedSubtableSaveResult(response, context) {
    if (!context.subtableFields) return response;
    const processed = entries => Object.fromEntries(Object.entries(entries ?? {})
        .filter(([field]) => context.subtableFields.includes(field)));
    return { ...response, subtables: processed(response?.subtables), subtable_row_ids: processed(response?.subtable_row_ids) };
}

// A save on another tab cannot repair these failures: its server-side field processing
// is separate. Keep them with the draft across queues until each failed tab succeeds.
function retainEditFormSaveFailures(form, context, saved, issues) {
    const response = form.save_result;
    const failures = editFormSaveFailures.get(form) ?? new Map();
    if (saved) failures.delete(context.tab);
    else {
        const failureIssues = issues.map(issue => ({ ...issue, tab: issue.tab ?? context.tab }));
        if (!failureIssues.some(issue => issue.severity === 'error')) {
            const message = response?.error?.message || 'Save failed';
            failureIssues.push({ severity: 'error', tab: context.tab,
                message: context.tabLabel ? context.tabLabel + ': ' + message : message });
        }
        failures.set(context.tab, { response, issues: failureIssues });
    }
    if (!failures.size) {
        editFormSaveFailures.delete(form);
        return;
    }
    editFormSaveFailures.set(form, failures);
    const outstanding = [...failures.values()];
    const firstFailure = outstanding[0];
    form.save_result = {
        ...response,
        success: false,
        error: {
            ...firstFailure.response?.error,
            message: firstFailure.response?.error?.message || firstFailure.issues.find(issue => issue.severity === 'error').message,
            details: Object.assign({}, ...outstanding.map(failure => failure.response?.error?.details ?? {})),
        },
        validation_issues: [
            ...(saved ? issues.map(issue => ({ ...issue, tab: issue.tab ?? context.tab })) : []),
            ...outstanding.flatMap(failure => failure.issues),
        ],
    };
}

// Merge into the captured draft without dispatching an active-form extension for an inactive form.
function reconcileSubtableSaveResult(response, submittedSubtables, form) {
    if (!form || !response) return;
    const subtableRowIds = response.subtable_row_ids ?? {};
    const subtableRows = response.subtables ?? {};
    Object.keys(subtableRowIds).forEach(field => {
        const rows = form.subtables?.[field];
        const rowMap = subtableRowIds[field] ?? {};
        if (!Array.isArray(rows)) return;
        rows.forEach(row => {
            const newId = rowMap[row.id];
            if (!newId) return;
            row.id = newId;
            row.is_new = false;
        });
    });

    Object.keys(subtableRows).forEach(field => {
        const serverRows = subtableRows[field] ?? [];
        if (!form.subtables) form.subtables = {};
        const existingRows = form.subtables[field] ?? [];
        const existingById = new Map(existingRows.map(row => [String(row.id), row]));
        const rowMap = subtableRowIds[field] ?? {};
        const submittedById = new Map((submittedSubtables?.[field] ?? []).map(row => [String(rowMap[row.id] ?? row.id), row]));
        const updatedRows = [];
        const serverIds = new Set();
        serverRows.forEach(row => {
            const id = String(row.id);
            serverIds.add(id);
            const localRow = existingById.get(id);
            const submittedRow = submittedById.get(id);
            if (submittedSubtables && submittedRow && !localRow) return; // removed while saving
            if (localRow) {
                Object.keys(row).forEach(col => {
                    if (!submittedSubtables || (submittedRow && JSON.stringify(localRow[col]) === JSON.stringify(submittedRow[col]))) {
                        localRow[col] = row[col];
                    }
                });
                updatedRows.push(localRow);
            } else updatedRows.push(row);
        });
        if (submittedSubtables) {
            existingRows.forEach(row => {
                if (serverIds.has(String(row.id))) return;
                const submittedRow = submittedById.get(String(row.id));
                const changed = submittedRow && Object.keys({ ...submittedRow, ...row }).some(col =>
                    col !== 'id' && col !== 'is_new' && JSON.stringify(row[col]) !== JSON.stringify(submittedRow[col]));
                if (!submittedRow || changed) updatedRows.push(row); // added/changed draft absent from old response
            });
        }
        existingRows.splice(0, existingRows.length, ...updatedRows);
        form.subtables[field] = existingRows;
    });
}

let getters = {
    is_saving_edit: (state) => state.edit_save_states.some(save => save.form === state.edit_data),
    active_save_signature: (state) => state.edit_save_states.find(save => save.form === state.edit_data)?.signature ?? '',
    pending_edit_save: (state) => state.edit_save_states.find(save => save.form === state.edit_data)?.pending ?? false,
    doubleCount: (state) => state.count * 2, //sample getter
    //return true if state.list_headers contains at least one non-empty search_value
    isListSearch: (state) => state.list_headers.some(h => hasListSearchValue(h.search_value)),
    //count of hchecked_rows but only true values
    countCheckedRows: (state) => Object.values(state.hchecked_rows).filter(v => v).length,
    // get checked rows for request as [id] => 1
    checkedRows: (state) => {
        let checked = {};
        Object.keys(state.hchecked_rows).forEach(id => {
            if (state.hchecked_rows[id]) checked[id] = 1;
        });
        return checked;
    },
    // get checked rows as comma-separated string
    checkedRowsCommas: (state) => {
        return Object.keys(state.hchecked_rows).filter(id => state.hchecked_rows[id]).join(',');
    },
    listRequestQuery: (state) => {
        // build request query from state.f, each parameter name should be int form "f[name]"
        let req = { is_list_edit: state.is_list_edit };
        if (state.is_initial_load) {
            // initial load - don't set filters, we'll get them from backend
        } else {
            req.dofilter = 1;
            req.scope = state.is_list_edit ? 'list_rows,lookups' : 'list_rows';
            Object.keys(state.f).forEach(key => {
                req['f[' + key + ']'] = state.f[key] ?? ''; //null to empty string
            });
        }
        // add related_id to request
        if (state.related_id) req.related_id = state.related_id;

        //add active search values from headers on explicit filter requests
        if (!state.is_initial_load) {
            state.list_headers.forEach(h => {
                if (hasListSearchValue(h.search_value)) req['search[' + h.field_name + ']'] = serializeListSearchValue(h.search_value);
            });
        }
        return req;
    },
    lookupByDef: (state) => (def) => {
        //return lookup array options by field definition
        var lookup_model = def.lookup_model;
        if (lookup_model) {
            return state.lookups[lookup_model] ?? [];
        }
        var lookup_tpl = def.lookup_tpl;
        if (lookup_tpl) {
            return state.lookups[lookup_tpl] ?? [];
        }
        if (state.form_tabs?.length) {
            const tabbed = state.showform_fields_tabs ?? {};
            for (const key of Object.keys(tabbed)) {
                const defs = tabbed[key] ?? [];
                const match = defs.find(item => item.field === def.field_name);
                if (!match) continue;
                if (match.lookup_model) return state.lookups[match.lookup_model] ?? [];
                if (match.lookup_tpl) return state.lookups[match.lookup_tpl] ?? [];
            }
        }
    },
    fieldsToTree: () => (arr) => {
        //return hierarchial array of plain array of fields:
        let root = []; // This will hold the top-level elements
        let stack = [root]; // Stack to manage hierarchy, starting with the root

        arr.forEach(item => {
            if (item.type === 'row' || item.type === 'col' || item.type === 'fieldset') {
                // If the item is a row or column, it's a new parent, so create a children array in it
                item.children = [];

                // Get the current parent from the stack and add this item to its children
                let parent = stack[stack.length - 1];
                parent.push(item);

                // Push this item onto the stack so it becomes the new current parent
                stack.push(item.children);
            } else if (item.type === 'row_end' || item.type === 'col_end' || item.type === 'fieldset_end') {
                // If it's an end marker, just pop the last parent from the stack
                stack.pop();
            } else {
                // If it's any other item, it's a child of the current parent
                let parent = stack[stack.length - 1];
                parent.push(item);
            }
        });

        //console.log('arrayToTree', root);
        return root;
    },
    treeShowFields: (state) => {
        return state.fieldsToTree(state.show_fields);
    },
    treeShowFormFields: (state) => {
        return state.fieldsToTree(state.showform_fields);
    },
    activeFormTab: (state) => {
        if (!state.form_tabs?.length) return '';
        const defaultTab = state.form_tabs[0]?.tab ?? '';
        return state.form_tabs.some(tab => tab.tab === state.current_form_tab) ? state.current_form_tab : defaultTab;
    },
    tabbedShowFields: (state) => {
        if (!state.form_tabs?.length) return state.show_fields;
        const defaultTab = state.form_tabs[0]?.tab ?? '';
        const activeTab = state.form_tabs.some(tab => tab.tab === state.current_form_tab) ? state.current_form_tab : defaultTab;
        return state.show_fields_tabs?.[activeTab] ?? state.show_fields;
    },
    tabbedShowFormFields: (state) => {
        if (!state.form_tabs?.length) return state.showform_fields;
        const defaultTab = state.form_tabs[0]?.tab ?? '';
        const activeTab = state.form_tabs.some(tab => tab.tab === state.current_form_tab) ? state.current_form_tab : defaultTab;
        return state.showform_fields_tabs?.[activeTab] ?? state.showform_fields;
    },
    allShowFormFields: (state) => {
        if (!state.form_tabs?.length) return state.showform_fields;
        const tabbed = state.showform_fields_tabs ?? {};
        const merged = [];
        Object.keys(tabbed).forEach(key => {
            const defs = tabbed[key] ?? [];
            defs.forEach(def => merged.push(def));
        });
        if (!merged.length) return state.showform_fields;
        return merged;
    },
    treeTabbedShowFields: (state) => {
        if (!state.form_tabs?.length) return state.fieldsToTree(state.show_fields);
        const defaultTab = state.form_tabs[0]?.tab ?? '';
        const activeTab = state.form_tabs.some(tab => tab.tab === state.current_form_tab) ? state.current_form_tab : defaultTab;
        return state.fieldsToTree(state.show_fields_tabs?.[activeTab] ?? state.show_fields);
    },
    treeTabbedShowFormFields: (state) => {
        if (!state.form_tabs?.length) return state.fieldsToTree(state.showform_fields);
        const defaultTab = state.form_tabs[0]?.tab ?? '';
        const activeTab = state.form_tabs.some(tab => tab.tab === state.current_form_tab) ? state.current_form_tab : defaultTab;
        return state.fieldsToTree(state.showform_fields_tabs?.[activeTab] ?? state.showform_fields);
    },
    savedStatus: (state) => {
        let sr = state.edit_data?.save_result ?? null;
        if (!sr) return null; // no save initiated yet
        return !!sr.id && sr.success !== false && !sr.error && !(sr.validation_issues ?? []).some(issue => issue.severity !== 'warning');
    },
    savedErrorMessage: (state) => {
        return state.edit_data?.save_result?.error?.message ?? '';
    }
};

//merge in fwStoreGetters if defined
getters = mergeStoreDefaults(getters, fwStoreGetters);

let actions = {
    initApi: createStoreInitApiAction(),
    startItemLoading() {
        if (this.loading_progress_timer) {
            clearInterval(this.loading_progress_timer);
        }
        if (this.loading_progress_timeout) {
            clearTimeout(this.loading_progress_timeout);
        }
        this.is_loading_item = true;
        this.loading_progress = 10;
        const start = Date.now();
        this.loading_progress_timer = setInterval(() => {
            const elapsed = Date.now() - start;
            const next_progress = Math.min(85, 10 + (elapsed / 1200) * 30);
            this.loading_progress = Math.max(this.loading_progress, next_progress);
        }, 120);
    },
    finishItemLoading() {
        if (this.loading_progress_timer) {
            clearInterval(this.loading_progress_timer);
            this.loading_progress_timer = null;
        }
        this.loading_progress = 100;
        this.loading_progress_timeout = setTimeout(() => {
            this.is_loading_item = false;
            this.loading_progress = 0;
        }, 180);
    },
    handleError(error, caller, is_silent) {
        let err_msg = error.body?.error?.message ?? 'server error';
        console.error('handleError for', caller, ":", err_msg);
        if (!is_silent) {
            //console.error(error);
            Toast(err_msg, { theme: 'text-bg-danger' });
        }
    },
    initFormTabFromLocation() {
        const params = new URLSearchParams(window.location.search);
        const tab = params.get('tab');
        if (tab !== null) {
            this.current_form_tab = tab;
        }
    },
    syncFormTab() {
        if (!this.form_tabs?.length) {
            this.current_form_tab = '';
            return;
        }
        const tabs = this.form_tabs.map(tab => tab.tab ?? '');
        const nextTab = tabs.includes(this.current_form_tab) ? this.current_form_tab : (tabs[0] ?? '');
        this.current_form_tab = nextTab;
    },
    buildScreenUrl(screen, id, tab = null) {
        let suffix = '';
        if (screen == 'view') {
            suffix = '/' + id;
        } else if (screen == 'edit') {
            suffix = '/' + (id ? id + '/edit' : 'new');
        }
        const params = new URLSearchParams();
        if (tab && (screen === 'view' || screen === 'edit')) {
            params.set('tab', tab);
        }
        if (this.return_url) {
            params.set('return_url', this.return_url);
            if (this.return_title) {
                params.set('return_title', this.return_title);
            }
        }
        const query = params.toString();
        if (query) {
            suffix += '?' + query;
        }
        return this.base_url + suffix;
    },
    updateTabUrl(tab) {
        if (!this.current_screen || this.current_screen === 'list') return;
        const nextUrl = this.buildScreenUrl(this.current_screen, this.current_id, tab);
        window.history.replaceState({ screen: this.current_screen, id: this.current_id }, '', nextUrl);
    },
    setFormTab(tab) {
        if (this.current_form_tab === tab) return;
        this.current_form_tab = tab;
        this.updateTabUrl(tab);
    },
    // screen navigation
    async setCurrentScreen(screen, id, options = {}) {
        // console.log("setCurrentScreen:", screen, id);
        const previous_screen = this.current_screen;
        const is_same_mode = (previous_screen === screen) && (screen === 'view' || screen === 'edit');
        this.current_screen = screen;
        this.current_id = id ?? 0;
        let suffix = '';
        if (screen == 'view') {
            suffix = '/' + id;
            if (!is_same_mode) {
                this.edit_data = null;
            }
        } else if (screen == 'edit') {
            suffix = '/' + (id ? id + '/edit' : 'new');
            if (!id) {
                this.edit_data = { i: {} };
            } else if (!is_same_mode) {
                this.edit_data = null;
            }
        }
        const nextTab = this.form_tabs?.length ? (this.current_form_tab || this.form_tabs[0]?.tab) : '';
        const nextUrl = this.buildScreenUrl(screen, id, nextTab);
        const historyState = { screen: screen, id: this.current_id };
        if (!options.skipHistory) {
            if (options.replace) {
                window.history.replaceState(historyState, '', nextUrl);
            } else {
                window.history.pushState(historyState, '', nextUrl);
            }
        }
        window.setTimeout(() => document.dispatchEvent(new CustomEvent('fw-page-change')), 0);
        this.is_list_edit_pane = false;
        if (id && (screen == 'view' || screen == 'edit')) {
            this.startItemLoading();
            await this.loadItem(id, screen);
        }
    },
    async openListScreen() {
        await this.setCurrentScreen('list');
    },
    async openViewScreen(id) {
        await this.setCurrentScreen('view', id);
    },
    async openEditScreen(id) {
        await this.setCurrentScreen('edit', id);
    },
    // update list_headers from showform_fields after loadIndex
    enrichEditableListHeaders() {
        const allShowFormFields = this.allShowFormFields ?? this.showform_fields;
        if (!allShowFormFields) return;

        // convert showform_fields array to lookup hashtable with keys as field
        let hfields = {};
        allShowFormFields.forEach(def => {
            if (def.field && !hfields[def.field]) {
                hfields[def.field] = def;
            }
        });

        this.list_headers = this.list_headers.map(header => {
            let field_name = header.field_name;
            let def = hfields[field_name] ?? null;
            if (!def) {
                //if no editable field definition found - make as read-only
                header.is_ro = true;
                return header;
            }

            let def_type = def.type;
            header.input_type = def_type;
            if (!this.list_editable_def_types.includes(def_type) || def.immutable_on_edit) header.is_ro = true;

            //add all other def attributes to header (if not exists in header yet)
            Object.keys(def).forEach(attr => {
                if (header[attr] === undefined || header[attr] === null || header[attr] === '') {
                    header[attr] = def[attr];
                }
            });

            return header;
        });
    },

    //save to store each key from data if such key exists in store
    saveToStore: createStoreSaveToStoreAction({ allowStoreMerge: true }),

    // set defaults
    applyDefaultsAfterLoad(data) {
        this.uioptions.list.table.isButtonsLeft = this.uioptions.list.table.isButtonsLeft ?? this.global.is_list_btn_left;
        this.list_user_view.density = this.list_user_view.density ?? 'table-sm';
        this.syncFormTab();
        this.is_initial_load = false; // reset initial load flag
        if (data.showform_fields) {
            this.enrichEditableListHeaders();
        }
    },

    // called when app mounted
    async afterMounted() {
        //show flash success or error message if exists
        if (this.flash.success)
            Toast(this.flash.success, { theme: 'text-bg-success' });
        if (this.flash.error)
            Toast(this.flash.error, { theme: 'text-bg-danger' });
        this.initFormTabFromLocation();
    },

    // load init and lookup scopes only
    async loadInitial() {
        try {
            const data = await this.api.get('', { query: { scope: 'init,lookups' } });
            //console.log('loadInitial data', data);

            this.saveToStore(data);
            this.applyDefaultsAfterLoad(data);
        } catch (error) {
            this.handleError(error, 'loadInitial');
        }
    },

    // set one or multiple filter values and reload list
    setFilters(filters) {
        //console.log('setFilters', filters);
        //whenever filters changed - reset page to first (if no specific page set)
        if (filters.pagenum === undefined) filters.pagenum = 0;
        //merge filters into state.f
        this.$state.f = { ...this.$state.f, ...filters };
        this.loadIndexDebounced();
    },
    //save user view settings (density)
    async setListDensity(density) {
        this.list_user_view.density = density;
        return this.saveUserViews({ density: density });
    },
    async reloadIndex() {
        if (this.loadIndexDebouncedTimeout) clearTimeout(this.loadIndexDebouncedTimeout);
        this.is_initial_load = true;
        await this.loadIndex();
    },
    // load data debounced
    async loadIndexDebounced() {
        // debounce loadIndex
        if (this.loadIndexDebouncedTimeout) clearTimeout(this.loadIndexDebouncedTimeout);
        this.loadIndexDebouncedTimeout = setTimeout(() => {
            this.loadIndex();
        }, 100);
    },
    // load data
    async loadIndex() {
        try {
            this.is_loading_index = true;

            const req = this.listRequestQuery;
            const data = await this.api.get('', { query: req });
            //console.log('loadIndex data', data);
            this.is_loading_index = false;

            this.saveToStore(data);
            this.applyDefaultsAfterLoad(data);
            this.onLoadIndexSuccess();

        } catch (error) {
            this.handleError(error, 'loadIndex');
        } finally {
            this.is_loading_index = false;
        }
    },
    onLoadIndexSuccess() { }, // hook for custom actions after loadIndex
    //load single item for view/edit
    async loadItem(id, mode, extraQuery = null) {
        try {
            if (!this.is_loading_item) {
                this.startItemLoading();
            }
            let query = {};
            if (mode == 'edit') {
                query.mode = mode;
            }
            if (extraQuery) {
                query = { ...query, ...extraQuery };
            }
            const options = Object.keys(query).length ? { query: query } : undefined;

            const data = await this.api.get(id, options);
            //console.log('loadItem data', data);
            this.edit_data = data;
            window.setTimeout(() => document.dispatchEvent(new CustomEvent('fw-page-change')), 0);

        } catch (error) {
            this.handleError(error, 'loadItem');
        } finally {
            this.finishItemLoading();
        }
    },
    // load next/prev id related to id from current list
    async getNextID(id, is_prev) {
        try {
            const data = await this.api.get('/(Next)/' + id, { query: { prev: is_prev ? 1 : 0 } });

            return data.id;

        } catch (error) {
            this.handleError(error, 'getNext');
            throw error;
        }
    },
    //when custom cell button clicked
    async onCellBtnClick({ event, row, col }) {
        console.log('onCellBtnClick:', event, row[this.field_id], col.field);
    },
    // helper to handle custom URL click from row button
    async customUrlClick(id, url, post) {
        try {
            const api = mande(url + '/' + id);
            let response;
            if (post) {
                response = await api.post('', { XSS: this.XSS });
            } else {
                response = await api.get('');
            }
            if (response?.message) {
                Toast(response.message, { theme: 'text-bg-success' });
            }

            this.reloadIndex();
        } catch (error) {
            console.error(error);
            this.handleError(error, 'onRowBtnCustomClick');
        }
    },
    // event contains: url, post, e
    async onRowBtnCustomClick(row, event) {
        if (event.url) {
            await this.customUrlClick(row[this.field_id], event.url, event.post);            
        } else {
            console.log('onRowBtnCustomClick:', row, event);
        }
    },
    async onRowDblClick(row) {
        console.log('onRowDblClick:', row);
    },
    async onCellKeyup(event, row, col) {
        //console.log('onCellKeyup:', event, row, col);
    },
    //format cell value for display if header field is in view_list_custom
    cellFormatter(row, header) {
        if (header.field_name == "status") {
            // default - format status as badge
            const value = row[header.field_name];
            const status = this.lookups_std.statusf.find(s => s.id == value) || { iname: value, id: 0, bgcolor: 'text-bg-secondary' };
            return '<span class="badge ' + status.bgcolor + '" >'+AppUtils.htmlescape(status.iname)+'</span>';
        }
        return row[header.field_name] ?? '';
    },
    isTrustedListRenderer(header) {
        return header.field_name == "status" || this.view_list_custom_trusted.hasOwnProperty(header.field_name);
    },
    async saveCell(row, col) {
        let id = row[this.field_id];
        let id_name = id + '-' + col.field_name;

        this.cells_saving[id_name] = true; //set saving flag
        delete this.cells_errors[id_name]; //clear errors if any

        let field_name = col.field_name;
        let value = row[field_name];

        let item = { [field_name]: value };
        if (col.type == 'autocomplete') {
            //for autocomplete submit _iname instead of id value
            item[field_name] = null;
            item[field_name + '_iname'] = row[field_name + '_iname'];
        }

        try {
            const req = { item: item, XSS: this.XSS };
            //console.log('saveCell req', id, req);
            const response = await this.api.patch(id, req);
            //console.log('saveCell response', response);

            //remove saving flag after 5sec
            setTimeout(() => {
                delete this.cells_saving[id_name];
            }, 5000);

        } catch (error) {
            delete this.cells_saving[id_name];

            let err_msg = error.body?.error?.message ?? 'Server Error';

            //check if we got required field error
            let is_required = error.body?.error?.details?.REQUIRED ?? false;
            if (is_required) {
                err_msg = 'Required field';
            }

            //check if we got specific field error code
            let field_err_code = error.body?.error?.details?.[col.field_name] ?? '';
            if (field_err_code && field_err_code !== true) {
                err_msg = window.fwConst.ERR_CODES_MAP[field_err_code] ?? 'Invalid';
            }

            this.cells_errors[id_name] = err_msg;
            this.handleError(error, 'saveCell', true);
        }
    },
    async deleteRow(id) {
        try {
            const req = { XSS: this.XSS };
            //console.log('deleteRow req', req);
            const response = await this.api.delete(id, { query: req });
            if (response?.error || response?.success === false) return false;
            return true;

        } catch (error) {
            this.handleError(error, 'deleteRow');
            return false;
        }
    },
    async deleteCheckedRows() {
        try {
            const req = { XSS: this.XSS, delete: true };
            req.cb = this.checkedRows;
            if (!Object.keys(req.cb).length) return; //no checked rows

            //console.log('deleteCheckedRows req', req);
            const response = await this.api.put(req);
            if (response?.error || response?.success === false) return false;

            //clear checked rows
            this.hchecked_rows = {};

        } catch (error) {
            this.handleError(error, 'deleteCheckedRows');
            return false;
        }
        await this.loadIndex();
        return true;
    },

    async customCheckedRows(url) {
        try {
            const req = { XSS: this.XSS };
            req.cb = this.checkedRows;
            if (!Object.keys(req.cb).length) return;

            const api = mande(url);
            const response = await api.post('', req);
            this.hchecked_rows = {};

            if (response?.message) {
                Toast(response.message, { theme: 'text-bg-success' });
            }

        } catch (error) {
            this.handleError(error, 'customCheckedRows');
        } finally {
            this.loadIndex();
        }
    },
    async restoreRow(id) {
        try {
            const req = { XSS: this.XSS };
            const response = await this.api.post('/(RestoreDeleted)/' + id, req);
        } catch (error) {
            this.handleError(error, 'restoreRow');
        }
    },

    // *** list edit pane support ***
    clearEditData() {
        this.edit_data = null;
    },
    async openEditPane(id) {
        this.edit_data = null;
        this.is_list_edit_pane = true;
        await this.loadItem(id, 'edit'); //load into fwStore.edit_data
    },
    // save edit form data debounced
    async saveEditDataDebounced(delay) {
        if (!delay) delay = 500;
        // debounce saveEditData
        const form = this.edit_data;
        if (form && !editFormSaveFailures.has(form)) form.save_result = {};
        const previous = editFormSaveDebounces.get(this);
        const requests = previous?.form === form ? previous.requests : [];
        const request = editFormSaveContext(this);
        const index = requests.findIndex(pending => pending.tab === request.tab);
        if (index >= 0) requests[index] = request;
        else requests.push(request);
        editFormSaveDebounces.set(this, { form, requests });
        if (this.saveEditDataDebouncedTimeout) clearTimeout(this.saveEditDataDebouncedTimeout);
        this.saveEditDataDebouncedTimeout = setTimeout(() => {
            editFormSaveDebounces.delete(this);
            if (form && this.edit_data === form) requests.forEach(context => this.saveEditData(context));
        }, delay);
    },
    // Save requests coalesce per tab, retaining the order of explicitly requested tabs.
    // A different form can save independently;
    // its request, response, and busy indicator never belong to the previously open form.
    async saveEditData(context = null) {
        const form = this.edit_data;
        if (!form || !this.canSaveForm()) return false;
        // Vue event handlers may pass a MouseEvent; only internally captured contexts override the active tab.
        const request = context?.[editFormSaveContextKey] === true ? context : editFormSaveContext(this);
        const signature = JSON.stringify([request.tab, editFormSaveSnapshot(form)]);
        const active = this.edit_save_states.find(save => save.form === form);
        if (active) {
            const index = active.requests.findIndex(pending => pending.tab === request.tab);
            if (active.signature === signature) {
                if (index >= 0) active.requests.splice(index, 1);
            } else if (index >= 0) active.requests[index] = request;
            else active.requests.push(request);
            active.pending = active.requests.length > 0;
            return false;
        }
        const api = this.api;
        const xss = this.XSS;
        const fieldId = this.field_id;
        const attachmentDefs = this.showform_fields.map(def => ({ field: def.field, att_post_prefix: def.att_post_prefix }));
        const wasNew = !form.id;
        this.edit_save_states.push({ form, signature, pending: false, requests: [request] });
        const saving = this.edit_save_states[this.edit_save_states.length - 1];
        try {
            while (true) {
                const currentRequest = saving.requests.shift();
                saving.pending = saving.requests.length > 0;
                const submitted = editFormSaveSnapshot(form);
                const submittedSignature = JSON.stringify(submitted);
                saving.signature = JSON.stringify([currentRequest.tab, submitted]);
                let saved = false;
                let changedDuringSave = false;
                let response;
                let serverError;
                try {
                    const req = { item: submitted.i, XSS: xss };
                    if (currentRequest.tab) req.tab = currentRequest.tab;
                    Object.keys(submitted.multi_rows ?? {}).forEach(field => {
                        const checkedRows = (submitted.multi_rows[field] ?? []).filter(row => row.is_checked);
                        if (checkedRows.length) req[field + '_multi'] = Object.fromEntries(checkedRows.map(row => [row.id, 1]));
                    });

                    // Keep the established item-FIELD[ID] and item-FIELD#ID[field] wire format.
                    Object.keys(submitted.subtables ?? {}).forEach(field => {
                        const rows = submitted.subtables[field] ?? [];
                        req['item-' + field] = {};
                        rows.forEach(row => {
                            const values = {};
                            Object.keys(row).forEach(col => {
                                if (col === 'id' || col === 'is_new') return;
                                const value = row[col];
                                if (Array.isArray(value) || (value && typeof value === 'object')) return;
                                values[col] = value;
                            });
                            req['item-' + field + '#' + row.id] = values;
                            req['item-' + field][row.id] = 1;
                        });
                    });
                    if (submitted.att_links?.length) req.att = Object.fromEntries(submitted.att_links.map(id => [id, 1]));
                    Object.keys(submitted.att_files ?? {}).forEach(field => {
                        const prefix = attachmentDefs.find(def => def.field === field)?.att_post_prefix ?? field;
                        const ids = submitted.att_files[field] ?? [];
                        if (ids.length) req[prefix] = { ...req[prefix], ...Object.fromEntries(ids.map(id => [id, 1])) };
                    });

                    response = await api.post(submitted.id, req);
                    changedDuringSave = JSON.stringify(editFormSaveSnapshot(form)) !== submittedSignature;
                    form.save_result = response;
                    saved = !response?.error && response?.success !== false && !this.formIssues(form).some(issue => issue.severity === 'error');
                    if (saved) {
                        // Even an inactive draft needs assigned IDs before it can be saved again.
                        if (response?.id && !form.id) {
                            form.id = response.id;
                            if (!form.i[fieldId]) form.i[fieldId] = response.id;
                        }
                        const subtableResponse = processedSubtableSaveResult(response, currentRequest);
                        if (this.edit_data === form) this.applySubtableSaveResult(subtableResponse, submitted.subtables ?? {}, form);
                        else reconcileSubtableSaveResult(subtableResponse, submitted.subtables ?? {}, form);
                    }
                } catch (error) {
                    saved = false;
                    form.save_result = error.body ?? { error: { message: 'Server error' } };
                    if (error.response >= 500) {
                        serverError = error;
                        this.handleError(error, 'saveEditData');
                    }
                    // A requested newer draft can retry after validation or transport failure.
                }

                retainEditFormSaveFailures(form, currentRequest, saved, this.formIssues(form));
                if (saving.pending) continue;
                if (!saved || editFormSaveFailures.has(form)) return serverError ?? false;
                if (this.edit_data !== form) return false;
                // Navigation/reload must not discard edits made after the submitted snapshot.
                if (changedDuringSave) {
                    if (wasNew && form.id && this.current_screen === 'edit') {
                        this.current_id = form.id;
                        window.history.replaceState({ screen: 'edit', id: form.id }, '', this.buildScreenUrl('edit', form.id, this.activeFormTab));
                    }
                    return;
                }

                if (this.current_screen === 'list') {
                    if (this.quick_edit_keep_context && this.is_list_edit_pane) {
                        try { await this.refreshQuickEditList(); }
                        catch (error) {
                            response.validation_issues = [...(response.validation_issues ?? []), { severity: 'warning', message: 'Saved, but the list could not refresh. Reload the list to see current values.' }];
                            this.handleError(error, 'refreshQuickEditList');
                        }
                    } else await this.loadIndex();
                } else {
                    const rr = form.route_return ?? '';
                    if (rr === 'New') {
                        Toast('Saved', { theme: 'text-bg-success' });
                        this.openEditScreen(0);
                    } else if (rr === 'Show') this.openViewScreen(response.id);
                    else if (rr === 'Index') this.openListScreen();
                    else if (wasNew && response.id) await this.openEditScreen(response.id);
                }
                if (!saving.pending) return;
            }
        } finally {
            const index = this.edit_save_states.indexOf(saving);
            if (index >= 0) this.edit_save_states.splice(index, 1);
        }
    },
    // A supplied snapshot preserves subsequent draft changes. Calls with one argument
    // retain the established authoritative refresh behavior for application extensions.
    applySubtableSaveResult(response, submittedSubtables = null, form = this.edit_data) {
        reconcileSubtableSaveResult(response, submittedSubtables, form);
    },

    // *** userlists support ***
    async saveCreateUserList() {
        try {
            const apiBase = mande(this.userlists_url);
            const req = { XSS: this.XSS, item: { entity: this.base_url, iname: this.userlists_new_name, item_id: this.checkedRowsCommas } };
            //console.log('saveCreateUserList req', req);
            const response = await apiBase.post('', req);
            //console.log('saveCreateUserList response', response);

            Toast("List created", { theme: 'text-bg-success' });

            //reload userslists via simply whole index reload
            this.reloadIndex();

        } catch (error) {
            this.handleError(error, 'saveCreateUserList');
            return error;
        }
    },
    async saveAddToUserList(userlists_id) {
        try {
            const apiBase = mande(this.userlists_url);
            const req = { XSS: this.XSS, item_id: this.checkedRowsCommas };
            //console.log('saveAddToUserList req', req);
            const response = await apiBase.post('/(AddToList)/' + userlists_id, req);
            //console.log('saveAddToUserList response', response);

            Toast("Added to List", { theme: 'text-bg-success' });

            //clear checked rows
            this.hchecked_rows = {};
            this.loadIndex();

        } catch (error) {
            this.handleError(error, 'saveAddToUserList');
            return error;
        }
    },
    //remove checked rows from currently loaded userlist
    async saveRemoveFromUserList() {
        try {
            const apiBase = mande(this.userlists_url);
            const req = { XSS: this.XSS, item_id: this.checkedRowsCommas };
            //console.log('saveRemoveFromUserList req', req);
            const response = await apiBase.post('/(RemoveFromList)/' + this.f.userlist, req);
            //console.log('saveRemoveFromUserList response', response);

            Toast("Removed from List", { theme: 'text-bg-success' });

            //clear checked rows
            this.hchecked_rows = {};
            this.reloadIndex();

        } catch (error) {
            this.handleError(error, 'saveRemoveFromUserList');
            return error;
        }
    },

    // *** userviews support ***
    async saveUserViews(params) {
        try {
            const req = { XSS: this.XSS, is_list_edit: this.is_list_edit, ...params };

            //console.log('saveUserViews req', req);
            const response = await this.api.post('/(SaveUserViews)', req);
            //console.log('saveUserViews response', response);

            if (!params.is_reset && !params.density && !params.load_id) {
                Toast("View saved", { theme: 'text-bg-success' });
            }

        } catch (error) {
            this.handleError(error, 'saveUserViews');
        } finally {
            //reload as whole as columns can be changed
            this.reloadIndex();
        }
    },
    async deleteUserViews(id) {
        try {
            const apiBase = mande(this.userlists_url);
            const req = { XSS: this.XSS };

            //console.log('deleteUserViews req', req);
            const response = await apiBase.delete(id, { query: req });
            //console.log('deleteUserViews response', response);

            Toast("View deleted", { theme: 'text-bg-success' });

        } catch (error) {
            this.handleError(error, 'deleteUserViews');
        } finally {
            //reload as whole as columns can be changed
            this.reloadIndex();
        }
    },

    async autocompleteOptions(q, url, model_name, id) {
        try {
            if (!url) url = this.base_url + '/(Autocomplete)';

            const apiBase = mande(url);
            const req = { q: q };
            if (model_name) req.model = model_name;
            if (id) req.id = id;

            const response = await apiBase.get('', { query: req });

            return response;

        } catch (error) {
            this.handleError(error, 'autocompleteOptions');
            return error;
        }
    }
};

actions = { ...actions, ...createInteractionActions() };
//merge in fwStoreActions if defined
actions = mergeStoreDefaults(actions, fwStoreActions);

const useFwStore = buildFwStore({ state, getters, actions });
window.fwStore = useFwStore; //make store available for components in html below
