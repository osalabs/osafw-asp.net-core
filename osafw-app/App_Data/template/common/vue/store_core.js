// shared Vue store setup helpers
function mergeStoreDefaults(base, override) {
    if (typeof override === 'undefined') return base;
    return AppUtils.deepMerge(base, override);
}

function createStoreInitApiAction() {
    return function () {
        this.api = mande(this.base_url);
    };
}

function createStoreSaveToStoreAction({ allowStoreMerge = false } = {}) {
    return function (data) {
        Object.keys(data).forEach(key => {
            if (allowStoreMerge && key === 'store') {
                try {
                    this.$state = AppUtils.deepMerge(this.$state, JSON.parse(data.store));
                } catch (e) {
                    console.error('Error parsing JSON for key:', key, e);
                }
            } else if (this.$state[key] !== undefined) {
                this.$state[key] = data[key];
            }
        });
    };
}

function buildFwStore({ state, getters, actions }) {
    return defineStore('fw', {
        state: () => (state),
        getters: getters,
        actions: actions,
    });
}
