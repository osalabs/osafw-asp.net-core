
// define some global constants
window.fwConst = {
    ERR_CODES_MAP: {
        REQUIRED: 'Required field',
        EXISTS: 'This name already exists in our database',
        WRONG: 'Invalid',
        EMAIL: 'Invalid Email',
    },
};

const useFwStore = defineStore('fw', {
  state: () => ({
    global: {}, //global config
    XSS: '', // token
    me_id: 0, // current user id
    access_level: 0, // user access level
    base_url: '', // base url for the controller
    list_title: '', //list screen title
    is_userlists: false,
    select_userlists: [],
    my_userlists: [],
    is_readonly: false,
    list_user_view: {}, // UserViews record for current controller

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
    list_headers: [], // list headers, array of {field_name:"", field_name_visible:"", is_sortable:bool, is_checked:bool, search_value:null|"", is_ro:bool, input_type:"input|select|date"}
    is_list_search_open: false, // true if list search is open by user
    count: 0, // total list rows count
    list_rows: [], // array of row objects to display, row can contain _meta object {is_ro:bool, ro_fields:[read only field names]}
    pager: [], // array of { pagenum:N, pagenum_show:N, is_cur_page:0|1, is_show_first:0|1, is_show_prev:0|1, is_show_next:0|1, pagenum_next:N}

    // edit form fields configuration
    list_editable_def_types: ['input', 'email', 'number', 'textarea', 'date_popup', 'datetime_popup', 'autocomplete', 'select', 'cb', 'radio', 'yesno'],
    list_editable_def_attrs: ['is_option0', 'is_option_empty', 'maxlength', 'min', 'max', 'step', 'placeholder', 'pattern', 'required', 'readonly', 'disabled'],
    showform_fields: [],
    is_list_edit_pane: false, // true if edit pane is open
    edit_data: null, // object for single item edit form {id:X, i:{}, add_users_id_name:'', upd_users_id_name:'', save_result:{}}}

    //standard lookups
    lookups_std: {
        statusf: [
            { id: '', iname: '- all -' },
            { id: 0, iname: 'Active' },
            { id: 10, iname: 'Inactive' }
        ],
        statusf_admin: [
            { id: '', iname: '- all -' },
            { id: 0, iname: 'Active' },
            { id: 10, iname: 'Inactive' },
            { id: 127, iname: '[Deleted]' }
        ]
        },
    //entity-related lookups
    lookups: {},

    //work vars
    hchecked_rows: {}, // array of checked rows {row.id => 1}
    loadIndexDebouncedTimeout: null,
    saveEditDataDebouncedTimeout: null,
    is_initial_load: true, //reset after initial load
    is_loading_index: false, //true while loading index data
    cells_saving: {}, // cells saving status {row.id_field => true}
    cells_errors: {}, // cells saving status {row.id_field => true}
  }),

  getters: {
      doubleCount: (state) => state.count * 2, //sample getter
      //return true if state.list_headers contains at least one non-empty search_value
      isListSearch: (state) => state.list_headers.some(h => h.search_value?.length),
      //count of hchecked_rows but only true values
      countCheckedRows: (state) => Object.values(state.hchecked_rows).filter(v => v).length,
      listRequestQuery: (state) => {
          // build request query from state.f, each parameter name should be int form "f[name]"
          let req = { dofilter: 1, is_list_edit: state.is_list_edit };
          if (!state.is_initial_load) {
              req.scope = 'list_rows'; // after initial load we only need list_rows
          }
          Object.keys(state.f).forEach(key => {
              if ((state.f[key]??'') !== '') {
                  req['f[' + key + ']'] = state.f[key];
              }              
          });
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
      treeShowFormFields: (state) => {
          //return hierarchial array of showform_fields:
          let root = []; // This will hold the top-level elements
          let stack = [root]; // Stack to manage hierarchy, starting with the root

          state.showform_fields.forEach(item => {
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

          console.log('treeShowFormFields', root);
          return root;          
      }
  },

  actions: {
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
            if (!def) return header;

            let def_type = def.type;
            header.input_type = def_type;
            if (!this.list_editable_def_types.includes(def_type)) header.is_ro = true;

            let lookup_model = def.lookup_model;
            if (lookup_model) header.lookup_model = lookup_model;

            let lookup_tpl = def.lookup_tpl;
            if (lookup_tpl) header.lookup_tpl = lookup_tpl;

            //add edit-related attributes to header, if exists in def: 
            this.list_editable_def_attrs.forEach(attr => {
                if (def[attr]) header[attr] = def[attr];
            });

            return header;
        });
    },

    // set one or multiple filter values and reload list
    setFilters(filters) {
        console.log('setFilters', filters);       
        //whenever filters changed - reset page to first (if no specific page set)
        if (filters.pagenum === undefined) filters.pagenum = 0;        
        //merge filters into state.f
        this.$state.f = { ...this.$state.f, ...filters };
        this.loadIndexDebounced();
    },
    //save user view settings (density)
    async setListDensity(density) {
        this.list_user_view.density = density;
        //save to backend
        const apiBase = mande(this.base_url);
        const req = { density: density, XSS: this.XSS };

        try {
            const data = await apiBase.post('/(SaveUserViews)', req);
        } catch (error) {
            console.error('setListDensity error:', error.body?.err_msg ?? 'server error');
            console.error(error);
            return error;
        }
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
            const apiBase = mande(this.base_url);

            const req = this.listRequestQuery;
            console.log('loadIndex req', req);
            const data = await apiBase.get('', { query: req });
            console.log('loadIndex data', data);

            //save to store each key from data if such key exists in store
            Object.keys(data).forEach(key => {
                if (this.$state[key] !== undefined) this.$state[key] = data[key];
            });

            // set defaults
            this.list_user_view.density = this.list_user_view.density ?? 'table-sm';
            
            this.is_initial_load = false; // reset initial load flag
            this.is_loading_index = false;

            if (data.showform_fields) {
                this.enrichEditableListHeaders();
            }

        } catch (error) {
            this.is_loading_index = false;
            console.error('loadIndex error:', error.body?.err_msg ?? 'server error');
            console.error(error);
            return error;
        }
    },
    async loadItem(id) {
        try {
            const apiBase = mande(this.base_url);

            const data = await apiBase.get(id);
            console.log('loadItem data', data);

            this.edit_data = data;

        } catch (error) {
            console.error('loadItem error:', error.body?.err_msg ?? 'server error');
            console.error(error);
            return error;
        }
    },
    async saveCell(row, col) {
        this.cells_saving[row.id + '-' + col.field_name] = true; //set saving flag
        delete this.cells_errors[row.id + '-' + col.field_name]; //clear errors if any

        let id = row.id;
        let field_name = col.field_name;
        let value = row[field_name];

        let item = { [field_name]: value };

        try {
            const apiBase = mande(this.base_url);
            
            const req = { item: item, XSS: this.XSS };
            console.log('saveCell req', id, req);
            const response = await apiBase.patch(id, req);
            console.log('saveCell response', response);

            //remove saving flag after 5sec
            setTimeout(() => {
                delete this.cells_saving[row.id+'-'+col.field_name];
            }, 5000);

        } catch (error) {
            delete this.cells_saving[row.id + '-' + col.field_name];

            let err_msg = error.body?.err_msg ?? 'Server Error';

            //check if we got required field error
            let is_required = error.body?.ERR?.REQUIRED ?? false;
            if (is_required) {
                err_msg = 'Required field';
            }

            //check if we got specific field error code
            let field_err_code = error.body?.ERR?.[col.field_name] ?? '';
            if (field_err_code && field_err_code!==true) {
                err_msg = window.fwConst.ERR_CODES_MAP[field_err_code] ?? 'Invalid';
            }

            this.cells_errors[row.id + '-' + col.field_name] = err_msg;
            console.error('saveCell error:', err_msg);
            //console.error(error);
            return error;
        }
    },
    async deleteRow(id) {
        try {
            const apiBase = mande(this.base_url);
            const req = { XSS: this.XSS };
            console.log('deleteRow req', req);
            const response = await apiBase.delete(id, { query: req });
            console.log('deleteRow response', response);

            //reload list to show changes
            this.loadIndex();

        } catch (error) {
            console.error('deleteRow error:', error.body?.err_msg ?? 'server error');
            console.error(error);
            return error;
        }
    },

    // *** list edit pane support ***
    clearEditData() {
        this.edit_data = null; 
    },
    async openEditPane(id) {
        this.edit_data = null;
        this.is_list_edit_pane = true;
        await this.loadItem(id); //load into fwStore.edit_data
    },
    // save edit form data debounced
    async saveEditDataDebounced() {
        // debounce saveEditData
        if (this.saveEditDataDebouncedTimeout) clearTimeout(this.saveEditDataDebouncedTimeout);
        this.saveEditDataDebouncedTimeout = setTimeout(() => {
            this.saveEditData();
        }, 500);
    },
    //save edit form data
    async saveEditData() {
        try {
            const apiBase = mande(this.base_url);
    
            const req = { item: this.edit_data.i, XSS: this.XSS };
            console.log('saveEditData req', req);
            const response = await apiBase.post(this.edit_data.id, req);
            console.log('saveEditData response', response);
            this.edit_data.save_result = response;
    
            //reload list to show changes
            this.loadIndex();
    
        } catch (error) {
            this.edit_data.save_result = error.body ?? { success:false, err_msg: 'server error' };
            console.error('saveEditData error:', error.body?.err_msg ?? 'server error');
            //console.error(error);
            return error;
        }
    },
  }
});
window.useFwStore=useFwStore; //make store available for components in html below