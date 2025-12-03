// define some global constants
window.fwConst = {
    ERR_CODES_MAP: {
        REQUIRED: 'Required field',
        EXISTS: 'This name already exists in our database',
        WRONG: 'Invalid',
        EMAIL: 'Invalid Email',
    },
};

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
    field_id: 'id', // model's id field name
    view_list_custom: [], // used for cellFormatter
    list_headers: [], // list headers, array of {field_name:"", field_name_visible:"", is_sortable:bool, is_checked:bool, search_value:null|"", is_ro:bool, input_type:"input|select|date"}
    is_list_search_open: false, // true if list search is open by user
    count: 0, // total list rows count
    list_rows: [], // array of row objects to display, row can contain _meta object {is_ro:bool, ro_fields:[read only field names]}
    pager: [], // array of { pagenum:N, pagenum_show:N, is_cur_page:0|1, is_show_first:0|1, is_show_prev:0|1, is_show_next:0|1, pagenum_next:N}

    // edit form fields configuration
    list_editable_def_types: ['input', 'email', 'number', 'textarea', 'date_popup', 'datetime_popup', 'autocomplete', 'select', 'cb', 'radio', 'yesno'],
    show_fields: [],
    showform_fields: [],
    is_list_edit_pane: false, // true if edit pane is open
    edit_data: null, // object for single item edit form {id:X, i:{}, multi_rows:{}, subtables:{}, attachments:{}, att_links:[att_ids], add_users_id_name:'', upd_users_id_name:'', save_result:{}}}

    //standard lookups
    lookups_std: {
        statusf: [
            { id: 0, iname: 'Active', bgcolor: 'bg-primary' },
            { id: 10, iname: 'Inactive', bgcolor: 'bg-secondary' }
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
    cells_saving: {}, // cells saving status {row[id_field] => true}
    cells_errors: {}, // cells saving status {row[id_field] => true}
};

// merge in fwStoreState if defined
if (typeof fwStoreState !== 'undefined') {
    state = AppUtils.deepMerge(state, fwStoreState);
}

let getters = {
    doubleCount: (state) => state.count * 2, //sample getter
    //return true if state.list_headers contains at least one non-empty search_value
    isListSearch: (state) => state.list_headers.some(h => h.search_value?.length),
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
            req.scope = 'list_rows'; // after initial load we only need list_rows
            Object.keys(state.f).forEach(key => {
                req['f[' + key + ']'] = state.f[key] ?? ''; //null to empty string
            });
        }
        // add related_id to request
        if (state.related_id) req.related_id = state.related_id;

        //add search values from headers if search is open
        if (state.is_list_search_open) {
            state.list_headers.forEach(h => {
                if (h.search_value?.length) req['search[' + h.field_name + ']'] = h.search_value;
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
    },
    fieldsToTree: () => (arr) => {
        //return hierarchial array of plain array of fields:
        let root = []; // This will hold the top-level elements
        let stack = [root]; // Stack to manage hierarchy, starting with the root

        arr.forEach(item => {
            if (item.type === 'row' || item.type === 'col') {
                // If the item is a row or column, it's a new parent, so create a children array in it
                item.children = [];

                // Get the current parent from the stack and add this item to its children
                let parent = stack[stack.length - 1];
                parent.push(item);

                // Push this item onto the stack so it becomes the new current parent
                stack.push(item.children);
            } else if (item.type === 'row_end' || item.type === 'col_end') {
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
    savedStatus: (state) => {
        let sr = state.edit_data?.save_result ?? null;
        if (!sr) return null; // no save initiated yet
        return !(!sr.id || sr.error); //saved when we have id and no error
    },
    savedErrorMessage: (state) => {
        return state.edit_data?.save_result?.error?.message ?? '';
    }
};

//merge in fwStoreGetters if defined
if (typeof fwStoreGetters !== 'undefined') {
    getters = AppUtils.deepMerge(getters, fwStoreGetters);
}

let actions = {
    initApi() {
        this.api = mande(this.base_url);
    },
    handleError(error, caller, is_silent) {
        let err_msg = error.body?.error?.message ?? 'server error';
        console.error('handleError for', caller, ":", err_msg);
        if (!is_silent) {
            //console.error(error);
            Toast(err_msg, { theme: 'text-bg-danger' });
        }
    },
    // screen navigation
    async setCurrentScreen(screen, id) {
        // console.log("setCurrentScreen:", screen, id);
        this.current_screen = screen;
        this.current_id = id;
        let suffix = '';
        if (screen == 'view') {
            suffix = '/' + id;
            this.edit_data = null;
        } else if (screen == 'edit') {
            suffix = '/' + (id ? id + '/edit' : 'new');
            this.edit_data = null;
            if (!id) this.edit_data = { i: {} };
        }
        window.history.pushState({ screen: screen, id: id }, '', this.base_url + suffix);
        this.is_list_edit_pane = false;
        if (id && (screen == 'view' || screen == 'edit')) {
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
        if (!this.showform_fields) return;

        // convert showform_fields array to lookup hashtable with keys as field
        let hfields = {};
        this.showform_fields.forEach(def => {
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
            if (!this.list_editable_def_types.includes(def_type)) header.is_ro = true;

            //add all other def attributes to header (if not exists in header yet)
            Object.keys(def).forEach(attr => {
                if (header[attr] === undefined) header[attr] = def[attr];
            });

            return header;
        });
    },

    // Date/Time formatting helpers (per-user formats & timezone)
    _userLocale() {
        // pick locale by user formats: MDY->en-US, DMY->en-GB
        const isDMY = (this.global.date_format ?? 0) == 10;
        return isDMY ? 'en-GB' : 'en-US';
    },
    _is24h() {
        return (this.global.time_format ?? 0) == 10;
    },
    _timeZone() {
        return this.global.timezone || 'UTC';
    },
    _dateFromServer(value) {
        if (!value) return null;
        if (value instanceof Date) return value;

        let t = String(value).trim();

        // already ISO with timezone or Z
        const hasTimezone = /T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(Z|[+-]\d{2}:?\d{2})$/i.test(t);
        if (!hasTimezone) {
            // legacy SQL formats - assume UTC coming from backend
            if (/^\d{4}-\d{2}-\d{2}$/.test(t)) {
                t = `${t}T00:00:00Z`;
            } else if (/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/.test(t)) {
                t = t.replace(' ', 'T') + 'Z';
            }
        }

        const d = new Date(t);
        if (Number.isNaN(d.getTime())) return null;

        return d;
    },
    formatDate(value) {
        const d = this._dateFromServer(value);
        if (!d) return value ?? '';
        return d.toLocaleDateString(this._userLocale(), { timeZone: this._timeZone() });
    },
    formatDateTime(value, withSeconds = true) {
        const d = this._dateFromServer(value);
        if (!d) return value ?? '';
        // build options per 12/24h, only format using user timezone provided by backend
        const opts = {
            timeZone: this._timeZone(),
            year: 'numeric', month: 'numeric', day: 'numeric',
            hour: '2-digit', minute: '2-digit',
            hour12: !this._is24h(),
        };
        if (withSeconds) opts.second = '2-digit';
        return d.toLocaleString(this._userLocale(), opts);
    },

    //save to store each key from data if such key exists in store
    saveToStore(data) {
        Object.keys(data).forEach(key => {
            if (key == "store") { // special store-level json - merge into $state itself
                try {
                    this.$state = AppUtils.deepMerge(this.$state, JSON.parse(data.store));
                } catch (e) {
                    console.error('Error parsing JSON for key:', key, e);
                }
            } else if (this.$state[key] !== undefined) {
                this.$state[key] = data[key];
            }
        });
    },

    // set defaults
    applyDefaultsAfterLoad(data) {
        this.uioptions.list.table.isButtonsLeft = this.uioptions.list.table.isButtonsLeft ?? this.global.is_list_btn_left;
        this.list_user_view.density = this.list_user_view.density ?? 'table-sm';
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
    async loadItem(id, mode) {
        try {
            this.is_loading_item = true;
            let q = {};
            if (mode == 'edit') {
                q = { query: { mode: mode } };
            }

            const data = await this.api.get(id, q);
            //console.log('loadItem data', data);
            this.edit_data = data;

        } catch (error) {
            this.handleError(error, 'loadItem');
        } finally {
            this.is_loading_item = false;
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
            const status = this.lookups_std.statusf.find(s => s.id == value) || { iname: value, id: 0, bgcolor: 'bg-secondary' };
            return '<span class="badge ' + status.bgcolor + '" >'+AppUtils.htmlescape(status.iname)+'</span>';
        }
        return row[header.field_name] ?? '';
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
            //console.log('deleteRow response', response);

        } catch (error) {
            this.handleError(error, 'deleteRow');
        }
    },
    async deleteCheckedRows() {
        try {
            const req = { XSS: this.XSS, delete: true };
            req.cb = this.checkedRows;
            if (!Object.keys(req.cb).length) return; //no checked rows

            //console.log('deleteCheckedRows req', req);
            const response = await this.api.put(req);
            //console.log('deleteCheckedRows response', response);

            //clear checked rows
            this.hchecked_rows = {};

        } catch (error) {
            this.handleError(error, 'deleteCheckedRows');
        } finally {
            //reload list to show changes
            this.loadIndex();
        }
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
        if (this.edit_data) this.edit_data.save_result = {};
        if (this.saveEditDataDebouncedTimeout) clearTimeout(this.saveEditDataDebouncedTimeout);
        this.saveEditDataDebouncedTimeout = setTimeout(() => {
            this.saveEditData();
        }, delay);
    },
    //save edit form data
    async saveEditData() {
        try {
            const req = { item: this.edit_data.i, XSS: this.XSS };
            // also submit checked multi_rows, if form has any
            Object.keys(this.edit_data.multi_rows ?? {}).forEach(field => {
                let rows = this.edit_data.multi_rows[field] ?? [];
                let checked_rows = rows.filter(row => row.is_checked);
                if (checked_rows.length) {
                    req[field + '_multi'] = {};
                    checked_rows.forEach(row => {
                        req[field + '_multi'][row.id] = 1;
                    });
                };
            });

            //also submit subtables
            // row ids submitted as: item-FIELD[ID]=1
            // input name format: item-FIELD#ID[field_name]=value
            Object.keys(this.edit_data.subtables ?? {}).forEach(field => {
                let rows = this.edit_data.subtables[field] ?? [];
                if (rows.length) {
                    req['item-' + field] = {};
                }
                rows.forEach(row => {
                    req['item-' + field + '#' + row.id] = {};
                    Object.keys(row).forEach(col => {
                        if (col == 'id') return; // skip id field, skip arrays?  || Array.isArray(row[col])
                        req['item-' + field + '#' + row.id][col] = row[col];
                    });
                    req['item-' + field][row.id] = 1;
                });
            });

            //also submit attachments (att_links) as att[ID]=1
            if (this.edit_data.att_links?.length) {
                req.att = {};
                this.edit_data.att_links.forEach(att_id => {
                    req.att[att_id] = 1;
                });
            }

            //also submit attachments (att_files) as att_post_prefix[ID]=1 (or field_name[ID]=1)
            if (this.edit_data.att_files) {
                Object.keys(this.edit_data.att_files).forEach(field_name => {
                    // find att_post_prefix from field definition
                    let def = this.showform_fields.find(f => f.field == field_name);
                    let att_post_prefix = def?.att_post_prefix ?? field_name;

                    let att_ids = this.edit_data.att_files[field_name] ?? [];
                    if (att_ids.length) {
                        req[att_post_prefix] = req[att_post_prefix] || {};
                        att_ids.forEach(att_id => {
                            req[att_post_prefix][att_id] = 1;
                        });
                    }
                });
            }

            //console.log('saveEditData req', req);
            const response = await this.api.post(this.edit_data.id, req);
            //console.log('saveEditData response', response);
            this.edit_data.save_result = response;

            if (this.current_screen == 'list') {
                //reload list to show changes
                await this.loadIndex();
            } else {
                //after edit form saved - process route_return
                const rr = this.edit_data.route_return ?? '';
                if (rr == 'New') {
                    Toast("Saved", { theme: 'text-bg-success' });
                    this.openEditScreen(0);
                } else if (rr == 'Show') {
                    this.openViewScreen(response.id);
                } else if (rr == 'Index') {
                    this.openListScreen();
                } else {
                    if (!response.error && response.id && !this.edit_data.id) {
                        //just reload edit after add new
                        await this.openEditScreen(response.id);
                    }
                }
            }

        } catch (error) {
            this.edit_data.save_result = error.body ?? { error: 'server error' };
            if (error.response >= 500) {
                this.handleError(error, 'saveEditData');
                return error;
            }
            //400 errors are user validation
        }
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

//merge in fwStoreActions if defined
if (typeof fwStoreActions !== 'undefined') {
    actions = AppUtils.deepMerge(actions, fwStoreActions);
}

const useFwStore = defineStore('fw', {
    state: () => (state),
    getters: getters,
    actions: actions,
});
window.fwStore = useFwStore; //make store available for components in html below