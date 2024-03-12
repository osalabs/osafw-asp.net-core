// define apis via mande
const apiUsers = mande('/api/users'); // TODO REMOVE sample

const useFwStore = defineStore('fw', {
  state: () => ({
    global: {}, //global config
    XSS: '', // token
    base_url: '', // base url for the controller
    is_userlists: false,
    is_readonly: false,
    user_view: {}, // UserViews record for current controller

    // list filter, can contain: {pagenum:N, pagesize:N, sortby:'', sortdir:'asc|desc'}
    f: {
        pagenum: 0,
        pagesize: 25,
        sortby: '',
        sortdir: '',
    }, 
    related_id: 0, // related model id
    return_url: '', // return url if controller called from other place expecting user's return
    field_id: 'id', // model's id field name
    headers: [], // list headers, array of {field_name:"", field_name_visible:"", is_sortable:true|false, is_checked:true, search_value:null|""}
    headers_search: [], // list of search values for headers filter TODO REMOVE
    count: 0, // total list rows count
    list_rows: [],
    pager: [] // array of { pagenum:N, pagenum_show:N, is_cur_page:0|1, is_show_first:0|1, is_show_prev:0|1, is_show_next:0|1, pagenum_next:N}
  }),

  getters: {
    doubleCount: (state) => state.count * 2, //sample getter
  },

  actions: {
    // set one or multiple filter values and reload list
    setFilters(filters) {
        console.log('setFilters', filters);
        //merge filters into state.f
        this.$state.f = { ...this.$state.f, ...filters };
        this.loadIndex();
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
        console.error('setListDensity error:', error.body.err_msg ?? 'server error');
        console.error(error);
        //fw.error(error);
        return error;
      }
    },
    // load data
    async loadIndex() {
      try {
        const apiBase = mande(this.base_url);

        // build request query from state.f, each parameter name should be int form "f[name]"
        let req = { dofilter: 1 };
        Object.keys(this.f).forEach(key => {
            req['f['+key+']'] = this.f[key];
        });
        // add related_id to request
        if (this.related_id) req.related_id = this.related_id;
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
        console.error('loadIndex error:', error.body.err_msg??'server error');
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