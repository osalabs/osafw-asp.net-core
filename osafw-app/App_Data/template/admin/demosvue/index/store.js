//import { mande } from 'mande';

const apiUsers = Mande.mande('/api/users');

const useMainStore = Pinia.defineStore('main', {
  state() {
    return {
      count: 0 //sample store value
    };
  },
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