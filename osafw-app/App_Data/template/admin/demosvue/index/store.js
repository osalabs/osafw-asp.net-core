// define apis via mande
const apiUsers = mande('/api/users');

const useFwStore = defineStore('fw', {
  state: () => ({
    global: {}, //global config
    count: 0, //total list rows count
    list_rows: [],
    pager: [],
    f: [], //list filter
    related_id: 0,
    base_url: '',
    is_userlists: false,
    is_readonly: false,
    return_url: ''
  }),
  getters: {
    doubleCount: (state) => state.count * 2, //sample getter
  },
  actions: {
    //sample action
    increment(state) {
      this.count++;
    },
    // load data
    async loadIndex() {
      try {
        const apiIndex = mande(this.base_url);

        const data = await apiIndex.get();
        console.log('loadIndex data', data);

        //save to store each key from data if such key exists in store
        Object.keys(data).forEach(key => {
            if (this.$state[key] !== undefined) this.$state[key] = data[key];
        });

      } catch (error) {
        console.error('loadIndex error:', error.body.err_msg??'server error');
        console.error(error);
        //fw.error(error);
        return error;
      }
    },

    // sample async action
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