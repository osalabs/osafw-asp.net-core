// define apis via mande
const apiUsers = mande('/api/users');

const useMainStore = defineStore('main', {
  state: () => ({
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
window.useMainStore=useMainStore;