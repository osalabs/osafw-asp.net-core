// define apis via mande
const apiUsers = mande('/api/users'); // TODO REMOVE sample

const useFwStore = defineStore('fw', {
  state: () => ({
    global: {}, //global config
    XSS: '', // token
    access_level: 0, // user access level
    base_url: '', // base url for the controller
    is_userlists: false,
    select_userlists: [],
    my_userlists: [],
    is_readonly: false,
    user_view: {}, // UserViews record for current controller

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
    headers: [], // list headers, array of {field_name:"", field_name_visible:"", is_sortable:true|false, is_checked:true, search_value:null|""}
    is_list_search_open: false, // true if list search is open by user
    count: 0, // total list rows count
    list_rows: [],
    pager: [], // array of { pagenum:N, pagenum_show:N, is_cur_page:0|1, is_show_first:0|1, is_show_prev:0|1, is_show_next:0|1, pagenum_next:N}
    hchecked_rows: {}, // array of checked rows {row.id => 1}

    //common lookups
    lookups: {
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
    //work vars
    loadIndexDebouncedTimeout: null
  }),

  getters: {
      doubleCount: (state) => state.count * 2, //sample getter
      //return true if state.headers contains at least one non-empty search_value
      isListSearch: (state) => state.headers.some(h => h.search_value?.length),
      //count of hchecked_rows but only true values
      countCheckedRows: (state) => Object.values(state.hchecked_rows).filter(v => v).length,
      listRequestQuery: (state) => {
          // build request query from state.f, each parameter name should be int form "f[name]"
          let req = { dofilter: 1 };
          Object.keys(state.f).forEach(key => {
              if ((state.f[key]??'') !== '') {
                  req['f[' + key + ']'] = state.f[key];
              }              
          });
          // add related_id to request
          if (state.related_id) req.related_id = state.related_id;

          //add search values from headers if search is open
          if (state.is_list_search_open) {
              state.headers.forEach(h => {
                  if (h.search_value?.length) req['search[' + h.field_name + ']'] = h.search_value;
              });
          }
          return req;
      }
  },

  actions: {
    // set one or multiple filter values and reload list
    setFilters(filters) {
        console.log('setFilters', filters);
        //merge filters into state.f
        this.$state.f = { ...this.$state.f, ...filters };
        this.loadIndexDebounced();
    },
    //save user view settings (density)
    async setListDensity(density) {
        this.user_view.density = density;
        //save to backend
        const apiBase = mande(this.base_url);
        const req = { density: density, XSS: this.XSS };

        try {
            const data = await apiBase.post('/(SaveUserViews)', req);
        } catch (error) {
            console.error('setListDensity error:', error.body?.err_msg ?? 'server error');
            console.error(error);
            //fw.error(error);
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
            this.user_view.density = this.user_view.density ?? 'table-sm';

        } catch (error) {
            console.error('loadIndex error:', error.body?.err_msg??'server error');
            console.error(error);
            //fw.error(error);
            return error;
        }
    },

    // sample async action TODO REMOVE
    async registerUser(login, password) {
        try {
            this.userData = await apiUsers.post({ login, password });
            fw.ok(`"Welcome back ${this.userData.name}!"`);
        } catch (error) {
            fw.error(error);
            return error;
        }
    },
  }
});
window.useFwStore=useFwStore; //make store available for components in html below