// minimal store for simple Vue screens
// imports are done in /layout/vue/sys_footer.html
<~/common/vue/store_core.js>

let state = {
    base_url: '',
    api: null,
    data: null,
    is_loading: true,
    error: null,

    count: 0, // total list rows count
};

// merge in fwStoreState if defined
state = mergeStoreDefaults(state, fwStoreState);

let getters = {
    doubleCount: (state) => state.count * 2, //sample getter
};

//merge in fwStoreGetters if defined
getters = mergeStoreDefaults(getters, fwStoreGetters);

let actions = {
    initApi: createStoreInitApiAction(),
    //save to store each key from data if such key exists in store
    saveToStore: createStoreSaveToStoreAction(),
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
actions = mergeStoreDefaults(actions, fwStoreActions);

const useFwStore = buildFwStore({ state, getters, actions });
window.fwStore = useFwStore;  //make store available for components in html below
