// minimal store for simple Vue screens
// imports are done in /layout/vue/sys_footer.html

let state = {
    base_url: '',
    api: null,
    data: null,
    is_loading: true,
    error: null,

    count: 0, // total list rows count
};

// merge in fwStoreState if defined
if (typeof fwStoreState !== 'undefined') {
    state = AppUtils.deepMerge(state, fwStoreState);
}

let getters = {
    doubleCount: (state) => state.count * 2, //sample getter
};

//merge in fwStoreGetters if defined
if (typeof fwStoreGetters !== 'undefined') {
    getters = AppUtils.deepMerge(getters, fwStoreGetters);
}

let actions = {
    initApi() {
        this.api = mande(this.base_url);
    },
    //save to store each key from data if such key exists in store
    saveToStore(data) {
        Object.keys(data).forEach(key => {
            if (this.$state[key] !== undefined) this.$state[key] = data[key];
        });
    },
    async loadData() {
        this.is_loading = true;
        try {
            const res = await this.api.get('');
            this.data = res;
            this.error = null;
        } catch (err) {
            console.error('loadData error', err);
            this.error = err.body?.error || 'server error';
        } finally {
            this.is_loading = false;
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
window.fwStore = useFwStore;  //make store available for components in html below
